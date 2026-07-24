namespace SvelteNet.AspNetCore;

/// <summary>Options specific to the Bun SSR backend.</summary>
public sealed class BunJsSsrOptions
{
	/// <summary>Bun executable name or absolute path.</summary>
	public string ExecutablePath { get; set; } = "bun";

	/// <summary>Maximum time allowed for one renderer process.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Trusted application origin used for relative server-side fetches. By default,
	/// SvelteNet uses an address reported by the running ASP.NET server.
	/// </summary>
	public Uri? BaseUrl { get; set; }

	/// <summary>Incoming headers forwarded to the trusted application origin.</summary>
	public ISet<string> ForwardHeaders { get; } =
		new HashSet<string>(["Authorization", "Cookie"], StringComparer.OrdinalIgnoreCase);
}
