namespace SvelteNet;

using System.Collections.Concurrent;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Runs the Svelte SSR bundle with Jint — a pure .NET JavaScript engine, so no Node.js
/// is needed at runtime. Engines are pooled: each engine caches its imported modules,
/// so after warm-up a render is just a function call (the Node pattern of import-once,
/// render-many). Jint engines are not thread-safe, hence one engine per concurrent render.
/// </summary>
/// <remarks>
/// <para>
/// Jint executes JavaScript synchronously and cannot suspend mid-script to await a .NET
/// task, so the fetch bridge has to block the thread running the render. That render is
/// therefore dispatched to a worker and gated by a semaphore sized to
/// <see cref="JintSsrOptions.MaxConcurrentRenders"/>: the request thread is released
/// immediately and the number of blocked threads stays bounded no matter how much traffic
/// arrives. The Node.js and Bun engines have no such constraint and are fully asynchronous.
/// </para>
/// <para>
/// Pooled engines keep their module graph — and therefore any module-level state in the
/// SSR bundle — alive across renders and across users. Component modules must not hold
/// per-request state at module scope. The Node.js and Bun engines start a fresh process
/// per render and do not share this behaviour.
/// </para>
/// </remarks>
public class JintSsrEngine : ISvelteSsrEngine, IDisposable
{
	private readonly SvelteOptions _options;
	private readonly JintSsrOptions _ssrOptions;
	private readonly ISvelteSsrFetchHandler? _fetchHandler;
	private readonly ILogger<JintSsrEngine> _logger;
	private readonly ConcurrentBag<Engine> _pool = new();
	private readonly SemaphoreSlim _renderSlots;
	private int _pooledCount;

	/// <summary>
	/// Cancellation for the render currently on this thread. Engines outlive a single
	/// request, so the fetch shim cannot close over the token it was created with — a
	/// pooled engine would keep using the first request's token forever. A render runs
	/// start to finish on one thread (Jint is synchronous), so thread-static is exact.
	/// </summary>
	[ThreadStatic]
	private static CancellationToken _currentRender;

	public JintSsrEngine(
		SvelteOptions options,
		ISvelteSsrFetchHandler? fetchHandler = null,
		JintSsrOptions? ssrOptions = null,
		ILogger<JintSsrEngine>? logger = null)
	{
		_options = options;
		_fetchHandler = fetchHandler;
		_ssrOptions = ssrOptions ?? new JintSsrOptions();
		_logger = logger ?? NullLogger<JintSsrEngine>.Instance;
		_renderSlots = new SemaphoreSlim(Math.Max(1, _ssrOptions.MaxConcurrentRenders));
	}

	public async ValueTask<SsrResult> RenderAsync(
		string componentModule,
		string renderModule,
		string? propsJson,
		CancellationToken cancellationToken = default)
	{
		await _renderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Off the request thread: the render (and any blocking fetch-bridge call it
			// makes) occupies a worker for its duration, never the caller.
			return await Task.Run(
				() => RenderCore(componentModule, renderModule, propsJson, cancellationToken),
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_renderSlots.Release();
		}
	}

	private SsrResult RenderCore(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken)
	{
		_currentRender = cancellationToken;
		try
		{
			if (!_pool.TryTake(out var engine)) engine = CreateEngine();
			else Interlocked.Decrement(ref _pooledCount);

			// Deliberately no try/finally around the render: an engine whose render threw
			// is discarded, not re-pooled — a module that fails evaluation stays errored in
			// the engine's module cache, so returning it would poison future renders.
			var component = engine.Modules.Import("./" + componentModule).Get("default");
			var renderFn = engine.Modules.Import("./" + renderModule).Get("renderComponent");

			// Drain pending continuations (e.g. Svelte lazily imports node:async_hooks and
			// installs AsyncLocalStorage in a .then) so async-context tracking — which
			// hydratable depends on — is in place before rendering starts.
			engine.Evaluate("Promise.resolve()").UnwrapIfPromise(cancellationToken);
			var props = propsJson is null ? JsValue.Undefined : new JsonParser(engine).Parse(propsJson);

			// renderComponent is async — await-expression trees resolve through the fetch bridge.
			var result = engine.Invoke(renderFn, component, props).UnwrapIfPromise(cancellationToken).AsObject();
			var head = result.Get("head");
			var body = result.Get("body");

			ReturnToPool(engine);
			return new SsrResult
			{
				Head = head.IsUndefined() ? string.Empty : head.ToString(),
				Body = body.IsUndefined() ? string.Empty : body.ToString()
			};
		}
		finally
		{
			_currentRender = default;
		}
	}

	/// <summary>
	/// ConcurrentBag.Count walks every thread-local queue, so the pool size is tracked
	/// separately rather than measured on each render.
	/// </summary>
	private void ReturnToPool(Engine engine)
	{
		if (Interlocked.Increment(ref _pooledCount) > _ssrOptions.MaxPooledEngines)
		{
			Interlocked.Decrement(ref _pooledCount);
			return;
		}

		_pool.Add(engine);
	}

	private Engine CreateEngine()
	{
		var serverDir = Path.Combine(_options.ContentRoot, _options.ServerOutput);
		var engine = new Engine(o => o
			.EnableModules(new SsrModuleLoader(serverDir))
			.TimeoutInterval(_ssrOptions.Timeout));
		engine.SetValue("console", new ConsoleWrapper(_logger));

		if (_fetchHandler is not null)
		{
			var handler = _fetchHandler;
			engine.SetValue("__sveltenetFetch", (string url, string method) =>
			{
				// Blocking is unavoidable here — Jint cannot suspend a running script to
				// await a task. RenderAsync keeps this off the request thread and caps how
				// many threads can be parked this way at once.
				var response = handler.HandleAsync(url, method, _currentRender)
					.AsTask()
					.GetAwaiter()
					.GetResult();
				return JsValue.FromObject(engine, new { status = response.Status, body = response.Body });
			});
			engine.Execute("""
				globalThis.fetch = (url, init) => {
					const result = __sveltenetFetch(String(url), (init && init.method) || 'GET');
					return Promise.resolve({
						ok: result.status >= 200 && result.status < 300,
						status: result.status,
						json: () => Promise.resolve(result.body == null ? undefined : JSON.parse(result.body)),
					});
				};
				""");
		}

		return engine;
	}

	public void Dispose()
	{
		_renderSlots.Dispose();
		GC.SuppressFinalize(this);
	}
}
