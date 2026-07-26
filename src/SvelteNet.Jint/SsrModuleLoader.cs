namespace SvelteNet;

using Jint;
using Jint.Runtime.Modules;

/// <summary>
/// Module loader for the SSR bundle: files resolve from the server output directory,
/// and node: builtins that Svelte's server runtime imports are served as shims.
/// </summary>
internal sealed class SsrModuleLoader : IModuleLoader
{
	// Svelte's async SSR uses AsyncLocalStorage for context tracking. Engines render
	// one tree at a time, so a persistent store provides the continuity it needs.
	private const string AsyncHooksShim = """
		export class AsyncLocalStorage {
			#store;
			getStore() { return this.#store; }
			run(store, fn, ...args) { this.#store = store; return fn(...args); }
			enterWith(store) { this.#store = store; }
			exit(fn, ...args) { this.#store = undefined; return fn(...args); }
			disable() { this.#store = undefined; }
		}
		export default { AsyncLocalStorage };
		""";

	private readonly DefaultModuleLoader _inner;

	public SsrModuleLoader(string basePath)
	{
		_inner = new DefaultModuleLoader(basePath);
	}

	public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
	{
		if (moduleRequest.Specifier == "node:async_hooks")
			return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);
		return _inner.Resolve(referencingModuleLocation, moduleRequest);
	}

	public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
	{
		if (resolved.ModuleRequest.Specifier == "node:async_hooks")
			return ModuleFactory.BuildSourceTextModule(engine, resolved, AsyncHooksShim);
		return _inner.LoadModule(engine, resolved);
	}
}
