namespace SvelteNet;

using System.Collections.Concurrent;
using Jint;
using Jint.Native;
using Jint.Native.Json;

/// <summary>
/// Runs the Svelte SSR bundle with Jint — a pure .NET JavaScript engine, so no Node.js
/// is needed at runtime. Engines are pooled: each engine caches its imported modules,
/// so after warm-up a render is just a function call (the Node pattern of import-once,
/// render-many). Jint engines are not thread-safe, hence one engine per concurrent render.
/// </summary>
public class JintSsrEngine : ISvelteSsrEngine
{
	private readonly SvelteOptions _options;
	private readonly JintSsrOptions _ssrOptions;
	private readonly ISvelteSsrFetchHandler? _fetchHandler;
	private readonly ConcurrentBag<Engine> _pool = new();

	public JintSsrEngine(
		SvelteOptions options,
		ISvelteSsrFetchHandler? fetchHandler = null,
		JintSsrOptions? ssrOptions = null)
	{
		_options = options;
		_fetchHandler = fetchHandler;
		_ssrOptions = ssrOptions ?? new JintSsrOptions();
	}

	public SsrResult Render(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default)
	{
		if (!_pool.TryTake(out var engine)) engine = CreateEngine();

		// Deliberately no try/finally: an engine whose render threw is discarded, not
		// re-pooled — a module that fails evaluation stays errored in the engine's
		// module cache, so returning it would poison future renders.
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

		if (_pool.Count < _ssrOptions.MaxPooledEngines) _pool.Add(engine);
		return new SsrResult
		{
			Head = head.IsUndefined() ? string.Empty : head.ToString(),
			Body = body.IsUndefined() ? string.Empty : body.ToString()
		};
	}

	private Engine CreateEngine()
	{
		var serverDir = Path.Combine(_options.ContentRoot, _options.ServerOutput);
		var engine = new Engine(o => o
			.EnableModules(new SsrModuleLoader(serverDir))
			.TimeoutInterval(_ssrOptions.Timeout));
		engine.SetValue("console", new ConsoleWrapper());

		if (_fetchHandler is not null)
		{
			var handler = _fetchHandler;
			engine.SetValue("__sveltenetFetch", (string url, string method) =>
			{
				var (status, body) = handler.Handle(url, method);
				return JsValue.FromObject(engine, new { status, body });
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
}
