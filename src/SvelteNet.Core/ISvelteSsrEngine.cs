namespace SvelteNet;


/// <summary>
/// Executes the Vite SSR bundle to render a component to HTML.
/// Register an implementation explicitly to enable SSR. SvelteNet ships an in-process
/// <see cref="JintSsrEngine"/> plus Node.js and Bun-backed engines in SvelteNet.AspNetCore.
/// Swap in a Node-sidecar implementation via DI if SSR throughput becomes a bottleneck.
/// </summary>
public interface ISvelteSsrEngine
{
	/// <param name="componentModule">Component module file path, relative to the server output directory.</param>
	/// <param name="renderModule">Render-helper module file path, relative to the server output directory.</param>
	/// <param name="propsJson">Props serialized as JSON, or null for no props.</param>
	SsrResult Render(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default);
}
