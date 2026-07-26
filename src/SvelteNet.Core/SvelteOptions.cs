namespace SvelteNet;

using System.Reflection;

public class SvelteOptions
{
	/// <summary>
	/// Assemblies scanned for [SvelteRemote] services, [SvelteComponent] models, and
	/// SveltePage types. Set automatically to the assembly calling AddSvelteNet; add
	/// more when those types live in other projects. Empty means "scan every loaded
	/// assembly" (the pre-scoped fallback).
	/// </summary>
	public IReadOnlyList<Assembly>? ApplicationAssemblies { get; set; }

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

	/// <summary>
	/// Enables an explicitly registered SSR engine globally. This does not register an
	/// engine; AddSvelteNet without an SSR builder remains client-only.
	/// </summary>
	public bool EnableSsr { get; set; } = true;

	/// <summary>Enables client mounting or hydration globally.</summary>
	public bool EnableCsr { get; set; } = true;

	/// <summary>Vite client build output. Must live under wwwroot so it is publicly served.</summary>
	public string ClientOutput { get; set; } = "wwwroot/client";

	private string? _clientPublicPath;

	/// <summary>
	/// URL prefix the client build is served from. Derived from <see cref="ClientOutput"/>
	/// by dropping the web root segment, so the two cannot silently disagree; set it
	/// explicitly only when assets are served from somewhere else (a CDN, say).
	/// </summary>
	public string ClientPublicPath
	{
		get => _clientPublicPath ?? DeriveClientPublicPath(ClientOutput);
		set => _clientPublicPath = value;
	}

	/// <summary>Web root directory name stripped from <see cref="ClientOutput"/>.</summary>
	public string WebRoot { get; set; } = "wwwroot";

	private string DeriveClientPublicPath(string clientOutput)
	{
		var normalized = clientOutput.Replace('\\', '/').Trim('/');
		var webRoot = WebRoot.Replace('\\', '/').Trim('/');

		// Output that is the web root itself is served from the site root.
		if (normalized.Equals(webRoot, StringComparison.OrdinalIgnoreCase)) return "/";
		if (normalized.StartsWith(webRoot + "/", StringComparison.OrdinalIgnoreCase))
			normalized = normalized[(webRoot.Length + 1)..];

		return "/" + normalized.Trim('/');
	}

	/// <summary>
	/// Vite SSR build output. Deliberately NOT under wwwroot — the server bundle
	/// should never be publicly downloadable.
	/// </summary>
	public string ServerOutput { get; set; } = ".svelte-net/server";

	public string DevServerUrl { get; set; } = "http://localhost:5173";

	/// <summary>
	/// When true, components are loaded from the Vite dev server and SSR is skipped.
	/// Set automatically from the hosting environment when using AddSvelteNet.
	/// </summary>
	public bool IsDev { get; set; }

	/// <summary>
	/// Whether UseSvelteNet also generates TypeScript types and scaffolds missing files
	/// at startup. Off by default — generation happens at build time via
	/// SvelteNet.Build.targets; enable this only when those targets aren't wired up.
	/// </summary>
	public bool? EnableScaffolding { get; set; }

	/// <summary>
	/// Enables the legacy reflection discovery/dispatch path for applications without
	/// generated descriptors. Disabled by default.
	/// </summary>
	public bool EnableReflectionFallback { get; set; }

	/// <summary>Stable Vite entry name for the package-owned client mount helper.</summary>
	public string MountModule => "sveltenet-client";

	/// <summary>Stable Vite entry name for the package-owned SSR render helper.</summary>
	public string RenderModule => "sveltenet-ssr";
}
