namespace SvelteNet;

/// <summary>Options specific to the in-process Jint SSR backend.</summary>
public sealed class JintSsrOptions
{
	/// <summary>Maximum time a single render may execute before it is aborted.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>Maximum number of warmed engines retained after concurrent renders.</summary>
	public int MaxPooledEngines { get; set; } = Math.Max(1, Environment.ProcessorCount);

	/// <summary>
	/// Maximum number of renders executing at once. Jint is a synchronous engine, so each
	/// in-flight render occupies one worker thread for its duration (including any awaited
	/// remote queries). Capping concurrency keeps that thread cost bounded instead of
	/// growing with request volume; excess renders queue rather than starving the pool.
	/// </summary>
	public int MaxConcurrentRenders { get; set; } = Math.Max(1, Environment.ProcessorCount);
}
