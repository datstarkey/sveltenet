namespace SvelteNet;

/// <summary>Output of a server-side render — mirrors the result of svelte/server's render().</summary>
public class SsrResult
{
	public string Head { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
}
