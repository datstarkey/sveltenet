namespace SvelteNet;

/// <summary>
/// In-process fetch bridge for SSR: when registered, the SSR engine exposes a
/// `fetch` shim that routes requests here instead of the network, so remote
/// queries (and await-expression components) execute during server rendering.
/// </summary>
public interface ISvelteSsrFetchHandler
{
	/// <returns>Status code and JSON body (null for empty/204 responses).</returns>
	(int Status, string? Body) Handle(string url, string method);
}
