namespace SvelteNet;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

public class SvelteRenderer
{
	private readonly SvelteOptions _options;
	private readonly ISvelteSsrEngine? _ssrEngine;
	private readonly ConcurrentDictionary<RenderType, ViteManifest> _manifests = new();

	public SvelteRenderer(SvelteOptions options, ISvelteSsrEngine? ssrEngine = null)
	{
		_options = options;
		_ssrEngine = ssrEngine;
	}

	public static readonly JsonSerializerOptions JsonOptions = SvelteJson.Options;

	/// <summary>Renders a component with a SvelteKit-style single "data" prop.</summary>
	public Task<SvelteRenderResult> RenderAsync(string component, object? data = null, string? elementId = null, CancellationToken cancellationToken = default)
	{
		return RenderAsync(new ComponentOptions
		{
			Component = component,
			ElementId = elementId,
			Props = data is null ? null : new Dictionary<string, object?> { ["data"] = data },
			CancellationToken = cancellationToken
		});
	}

	public async Task<SvelteRenderResult> RenderAsync(ComponentOptions component)
	{
		var plan = Plan(component);
		var ssr = plan.SsrEnabled
			? await RenderServerAsync(plan.ModuleKey, plan.PropsJson, component.CancellationToken).ConfigureAwait(false)
			: null;
		return Compose(plan, ssr);
	}

	/// <summary>
	/// Client-only render. Server rendering can await remote queries, so it has no
	/// synchronous form — when SSR is active for this component, use
	/// <see cref="RenderAsync(ComponentOptions)"/> rather than blocking a request thread.
	/// </summary>
	public SvelteRenderResult Render(string component, object? data = null, string? elementId = null)
	{
		return Render(new ComponentOptions
		{
			Component = component,
			ElementId = elementId,
			Props = data is null ? null : new Dictionary<string, object?> { ["data"] = data }
		});
	}

	/// <inheritdoc cref="Render(string, object?, string?)"/>
	public SvelteRenderResult Render(ComponentOptions component)
	{
		var plan = Plan(component);
		if (plan.SsrEnabled)
			throw new InvalidOperationException(
				$"Server rendering '{component.Component}' requires the asynchronous API — a server render can await " +
				"remote queries, and blocking a request thread on that work starves the thread pool. Use RenderAsync " +
				"(Razor: '@await Html.SvelteAsync(...)' or '@await Model.SvelteAsync()'), or disable SSR for this " +
				"component with ComponentOptions.Ssr = false.");

		return Compose(plan, ssr: null);
	}

	/// <summary>True when a server render would run for this component — an engine is registered and enabled, and this is not dev mode.</summary>
	public bool IsSsrEnabled(ComponentOptions component) =>
		_ssrEngine is not null && (component.Ssr ?? _options.EnableSsr) && !_options.IsDev;

	private readonly record struct RenderPlan(
		string ModuleKey,
		string ElementId,
		string? PropsJson,
		bool SsrEnabled,
		bool CsrEnabled);

	private RenderPlan Plan(ComponentOptions component)
	{
		var name = component.Component.Trim('/');
		return new RenderPlan(
			ModuleKey: $"{_options.PagesPath}/{name}.svelte",
			ElementId: component.ElementId ?? "svelte-" + name.Replace('/', '-').ToLowerInvariant(),
			PropsJson: component.Props is null ? null : JsonSerializer.Serialize(component.Props, JsonOptions),
			// SSR only runs against the built server bundle, so it is skipped in dev.
			SsrEnabled: IsSsrEnabled(component),
			CsrEnabled: component.Csr ?? _options.EnableCsr);
	}

	private SvelteRenderResult Compose(RenderPlan plan, SsrResult? ssr)
	{
		var html = new StringBuilder();
		html.Append($"<div id=\"{HtmlEncoder.Default.Encode(plan.ElementId)}\">");
		if (ssr is not null) html.Append(ssr.Body);
		html.Append("</div>");
		if (plan.CsrEnabled) html.Append(BuildClientScript(plan.ModuleKey, plan.ElementId, plan.PropsJson, hydrate: ssr is not null));

		var head = new StringBuilder();
		if (ssr?.Head is { Length: > 0 }) head.AppendLine(ssr.Head);
		head.Append(GetCssLinks(plan.ModuleKey));

		return new SvelteRenderResult { Html = html.ToString(), Head = head.ToString() };
	}

	private async ValueTask<SsrResult> RenderServerAsync(string moduleKey, string? propsJson, CancellationToken cancellationToken)
	{
		var manifest = GetManifest(RenderType.Ssr);
		var componentChunk = FindChunk(manifest, moduleKey)
			?? throw new InvalidOperationException($"'{moduleKey}' not found in the SSR manifest at '{_options.ServerOutput}'. Run the Vite SSR build or disable SSR.");
		var renderChunk = FindChunk(manifest, _options.RenderModule)
			?? throw new InvalidOperationException($"'{_options.RenderModule}' not found in the SSR manifest. Ensure it is included in the Vite SSR build inputs.");

		return await _ssrEngine!.RenderAsync(componentChunk.File!, renderChunk.File!, propsJson, cancellationToken).ConfigureAwait(false);
	}

	private string BuildClientScript(string moduleKey, string elementId, string? propsJson, bool hydrate)
	{
		string mountSrc, componentSrc;
		var viteClient = string.Empty;

		if (_options.IsDev)
		{
			var dev = _options.DevServerUrl.TrimEnd('/');
			viteClient = $"import \"{dev}/@vite/client\";\n\t";
			mountSrc = $"{dev}/@id/sveltenet/client";
			componentSrc = $"{dev}/{moduleKey}";
		}
		else
		{
			var manifest = GetManifest(RenderType.Csr);
			var mountChunk = FindChunk(manifest, _options.MountModule)
				?? throw new InvalidOperationException($"'{_options.MountModule}' not found in the client manifest at '{_options.ClientOutput}'. Ensure it is included in the Vite build inputs.");
			var componentChunk = FindChunk(manifest, moduleKey)
				?? throw new InvalidOperationException($"'{moduleKey}' not found in the client manifest at '{_options.ClientOutput}'. Run the Vite build.");
			var publicPath = _options.ClientPublicPath.TrimEnd('/');
			mountSrc = $"{publicPath}/{mountChunk.File}";
			componentSrc = $"{publicPath}/{componentChunk.File}";
		}

		var props = propsJson is null ? string.Empty : $",\n\t\tprops: {propsJson}";
		var elementIdJson = JsonSerializer.Serialize(elementId);

		return $$"""

<script type="module">
	{{viteClient}}import { mountComponent } from "{{mountSrc}}";
	import App from "{{componentSrc}}";
	mountComponent(App, {
		target: document.getElementById({{elementIdJson}}),
		hydrate: {{(hydrate ? "true" : "false")}}{{props}}
	});
</script>
""";
	}

	private string GetCssLinks(string moduleKey)
	{
		if (_options.IsDev) return string.Empty; // the Vite dev server injects styles itself

		var manifest = GetManifest(RenderType.Csr);
		var visited = new HashSet<string>(StringComparer.Ordinal);
		var css = new List<string>();

		void Walk(string key)
		{
			if (!visited.Add(key)) return;
			var chunk = FindChunk(manifest, key);
			if (chunk is null) return;
			if (chunk.Css is not null) css.AddRange(chunk.Css);
			foreach (var import in chunk.Imports ?? []) Walk(import);
		}

		Walk(moduleKey);
		var publicPath = _options.ClientPublicPath.TrimEnd('/');
		return string.Join(Environment.NewLine, css.Distinct().Select(c => $"<link rel=\"stylesheet\" href=\"{publicPath}/{c}\" />"));
	}

	/// <summary>
	/// Manifest lookup is ordinal: on a case-sensitive file system 'Index.svelte' and
	/// 'index.svelte' are different components, so a case-insensitive match could
	/// silently render the wrong one. The 'name' fallback covers Vite's stable entry
	/// names (sveltenet-client / sveltenet-ssr), which are not file paths.
	/// </summary>
	private static ViteChunk? FindChunk(ViteManifest manifest, string key)
	{
		if (manifest.TryGetValue(key, out var chunk)) return chunk;
		return manifest.FirstOrDefault(k => string.Equals(k.Value.Name, key, StringComparison.Ordinal)).Value;
	}

	private ViteManifest GetManifest(RenderType type)
	{
		return _manifests.GetOrAdd(type, t =>
		{
			var output = t switch
			{
				RenderType.Ssr => _options.ServerOutput,
				RenderType.Csr => _options.ClientOutput,
				_ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
			};

			var dir = Path.Combine(_options.ContentRoot, output);
			// Vite 5+ writes to .vite/manifest.json; older versions to manifest.json.
			var file = Path.Combine(dir, ".vite", "manifest.json");
			if (!File.Exists(file)) file = Path.Combine(dir, "manifest.json");
			if (!File.Exists(file))
				throw new FileNotFoundException($"No Vite manifest found in '{dir}'. Run the Vite build (set build.manifest = true), or enable dev mode.");

			return JsonSerializer.Deserialize<ViteManifest>(File.ReadAllText(file)) ?? new ViteManifest();
		});
	}
}
