namespace SvelteNet;

/// <summary>Options specific to the in-process Jint SSR backend.</summary>
public sealed class JintSsrOptions
{
	/// <summary>Maximum time a single render may execute before it is aborted.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>Maximum number of warmed engines retained after concurrent renders.</summary>
	public int MaxPooledEngines { get; set; } = Math.Max(1, Environment.ProcessorCount);
}
