namespace SvelteNet;

public class SvelteRenderResult
{
	/// <summary>Container element (with SSR content when enabled) plus the hydration script.</summary>
	public string Html { get; init; } = string.Empty;

	/// <summary>SSR &lt;svelte:head&gt; output plus stylesheet links. Render inside &lt;head&gt;.</summary>
	public string Head { get; init; } = string.Empty;
}
