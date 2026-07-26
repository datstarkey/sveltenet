namespace SvelteNet;

/// <summary>
/// Executes the Vite SSR bundle to render a component to HTML.
/// Register an implementation explicitly to enable SSR. SvelteNet ships an in-process
/// Jint engine plus Node.js and Bun-backed engines in SvelteNet.AspNetCore.
/// </summary>
/// <remarks>
/// Rendering is asynchronous because a server render can await remote queries, which in
/// turn hit databases and HTTP services. Implementations must never block the calling
/// thread waiting on that work — a synchronous JavaScript runtime should move its render
/// onto a bounded worker (as the Jint engine does) instead of blocking the request thread.
/// </remarks>
public interface ISvelteSsrEngine
{
	/// <param name="componentModule">Component module file path, relative to the server output directory.</param>
	/// <param name="renderModule">Render-helper module file path, relative to the server output directory.</param>
	/// <param name="propsJson">Props serialized as JSON, or null for no props.</param>
	/// <param name="cancellationToken">Cancels the render when the owning request is aborted.</param>
	ValueTask<SsrResult> RenderAsync(
		string componentModule,
		string renderModule,
		string? propsJson,
		CancellationToken cancellationToken = default);
}
