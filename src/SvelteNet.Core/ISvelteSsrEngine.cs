namespace SvelteNet;


/// <summary>
/// Executes the Vite SSR bundle to render a component to HTML.
/// The default implementation is <see cref="JintSsrEngine"/> (in-process, no Node required).
/// Swap in a Node-sidecar implementation via DI if SSR throughput becomes a bottleneck.
/// </summary>
public interface ISvelteSsrEngine
{
	/// <param name="componentModule">Component module file path, relative to the server output directory.</param>
	/// <param name="renderModule">Render-helper module file path, relative to the server output directory.</param>
	/// <param name="propsJson">Props serialized as JSON, or null for no props.</param>
	SsrResult Render(string componentModule, string renderModule, string? propsJson);
}
