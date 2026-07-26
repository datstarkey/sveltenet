namespace SvelteNet;

/// <summary>
/// In-process fetch bridge for SSR: when registered, the SSR engine exposes a
/// <c>fetch</c> shim that routes requests here instead of the network, so remote
/// queries (and await-expression components) execute during server rendering.
/// </summary>
public interface ISvelteSsrFetchHandler
{
	/// <summary>Resolves a request issued by component code during a server render.</summary>
	/// <param name="url">Application-relative request path, including any query string.</param>
	/// <param name="method">HTTP method.</param>
	/// <param name="cancellationToken">Cancels the call when the owning request is aborted.</param>
	ValueTask<SsrFetchResponse> HandleAsync(string url, string method, CancellationToken cancellationToken = default);
}

/// <summary>Result of an SSR bridge call: a status code and a JSON body (null for empty/204 responses).</summary>
public readonly record struct SsrFetchResponse(int Status, string? Body)
{
	public static SsrFetchResponse Json(string body) => new(200, body);

	public static SsrFetchResponse NoContent => new(204, null);

	public static SsrFetchResponse NotFound => new(404, null);

	public static SsrFetchResponse MethodNotAllowed => new(405, null);
}
