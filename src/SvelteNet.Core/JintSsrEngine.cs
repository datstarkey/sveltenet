namespace SvelteNet;

using Jint;
using Jint.Native;
using Jint.Native.Json;
using System.Collections.Concurrent;

/// <summary>
/// Runs the Svelte SSR bundle with Jint — a pure .NET JavaScript engine, so no Node.js
/// is needed at runtime. Engines are pooled: each engine caches its imported modules,
/// so after warm-up a render is just a function call (the Node pattern of import-once,
/// render-many). Jint engines are not thread-safe, hence one engine per concurrent render.
/// </summary>
public class JintSsrEngine : ISvelteSsrEngine
{
	private readonly SvelteOptions _options;
	private readonly ConcurrentBag<Engine> _pool = new();

	public JintSsrEngine(SvelteOptions options)
	{
		_options = options;
	}

	public SsrResult Render(string componentModule, string renderModule, string? propsJson)
	{
		if (!_pool.TryTake(out var engine)) engine = CreateEngine();

		// Deliberately no try/finally: an engine whose render threw is discarded, not
		// re-pooled — a module that fails evaluation stays errored in the engine's
		// module cache, so returning it would poison future renders.
		var component = engine.Modules.Import("./" + componentModule).Get("default");
		var renderFn = engine.Modules.Import("./" + renderModule).Get("renderComponent");
		var props = propsJson is null ? JsValue.Undefined : new JsonParser(engine).Parse(propsJson);

		var result = engine.Invoke(renderFn, component, props).AsObject();
		var head = result.Get("head");
		var body = result.Get("body");

		_pool.Add(engine);
		return new SsrResult
		{
			Head = head.IsUndefined() ? string.Empty : head.ToString(),
			Body = body.IsUndefined() ? string.Empty : body.ToString()
		};
	}

	private Engine CreateEngine()
	{
		var serverDir = Path.Combine(_options.ContentRoot, _options.ServerOutput);
		var engine = new Engine(o => o.EnableModules(serverDir));
		engine.SetValue("console", new ConsoleWrapper());
		return engine;
	}
}
