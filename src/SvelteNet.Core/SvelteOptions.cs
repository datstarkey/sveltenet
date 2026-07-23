namespace SvelteNet;

public class SvelteOptions
{
	/// <summary>
	/// Root directory the paths below are resolved against. Set automatically to the
	/// ASP.NET content root when using AddSvelteNet.
	/// </summary>
	public string ContentRoot { get; set; } = Directory.GetCurrentDirectory();

	/// <summary>
	/// Directory (relative to <see cref="ContentRoot"/>) containing the Svelte source files.
	/// Also the prefix of the Vite manifest keys.
	/// </summary>
	public string PagesPath { get; set; } = "Svelte";

	public bool EnableSsr { get; set; } = true;
	public bool EnableCsr { get; set; } = true;

	/// <summary>Vite client build output. Must live under wwwroot so it is publicly served.</summary>
	public string ClientOutput { get; set; } = "wwwroot/client";

	/// <summary>URL prefix the client build is served from (ClientOutput minus wwwroot).</summary>
	public string ClientPublicPath { get; set; } = "/client";

	/// <summary>
	/// Vite SSR build output. Deliberately NOT under wwwroot — the server bundle
	/// should never be publicly downloadable.
	/// </summary>
	public string ServerOutput { get; set; } = "svelte-ssr";

	public string DevServerUrl { get; set; } = "http://localhost:5173";

	/// <summary>
	/// When true, components are loaded from the Vite dev server and SSR is skipped.
	/// Set automatically from the hosting environment when using AddSvelteNet.
	/// </summary>
	public bool IsDev { get; set; }

	/// <summary>
	/// Whether UseSvelteNet generates TypeScript types and scaffolds missing files.
	/// Defaults to following <see cref="IsDev"/>.
	/// </summary>
	public bool? EnableScaffolding { get; set; }

	/// <summary>Manifest key of the scaffolded client mount helper.</summary>
	public string MountModule => $"{PagesPath}/mount.ts";

	/// <summary>Manifest key of the scaffolded SSR render helper.</summary>
	public string RenderModule => $"{PagesPath}/render.ts";
}
