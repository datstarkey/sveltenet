namespace SvelteNet;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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
	public SvelteRenderResult Render(string component, object? data = null, string? elementId = null)
	{
		return Render(new ComponentOptions
		{
			Component = component,
			ElementId = elementId,
			Props = data is null ? null : new Dictionary<string, object?> { ["data"] = data }
		});
	}

	public SvelteRenderResult Render(ComponentOptions component)
	{
		var name = component.Component.Trim('/');
		var moduleKey = $"{_options.PagesPath}/{name}.svelte";
		var elementId = component.ElementId ?? "svelte-" + name.Replace('/', '-').ToLowerInvariant();
		var propsJson = component.Props is null ? null : JsonSerializer.Serialize(component.Props, JsonOptions);
		var encodedElementId = HtmlEncoder.Default.Encode(elementId);

		// SSR only runs against the built server bundle, so it is skipped in dev.
		var ssrEnabled = _ssrEngine is not null
			&& (component.Ssr ?? _options.EnableSsr)
			&& !_options.IsDev;
		var csrEnabled = component.Csr ?? _options.EnableCsr;

		var ssr = ssrEnabled ? RenderServer(moduleKey, propsJson, component.CancellationToken) : null;

		var html = new StringBuilder();
		html.Append($"<div id=\"{encodedElementId}\">");
		if (ssr is not null) html.Append(ssr.Body);
		html.Append("</div>");
		if (csrEnabled) html.Append(BuildClientScript(moduleKey, elementId, propsJson, hydrate: ssr is not null));

		var head = new StringBuilder();
		if (ssr?.Head is { Length: > 0 }) head.AppendLine(ssr.Head);
		head.Append(GetCssLinks(moduleKey));

		return new SvelteRenderResult { Html = html.ToString(), Head = head.ToString() };
	}

	private SsrResult? RenderServer(string moduleKey, string? propsJson, CancellationToken cancellationToken)
	{
		var manifest = GetManifest(RenderType.Ssr);
		var componentChunk = FindChunk(manifest, moduleKey)
			?? throw new InvalidOperationException($"'{moduleKey}' not found in the SSR manifest at '{_options.ServerOutput}'. Run the Vite SSR build or disable SSR.");
		var renderChunk = FindChunk(manifest, _options.RenderModule)
			?? throw new InvalidOperationException($"'{_options.RenderModule}' not found in the SSR manifest. Ensure it is included in the Vite SSR build inputs.");

		return _ssrEngine!.Render(componentChunk.File!, renderChunk.File!, propsJson, cancellationToken);
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
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

	private static ViteChunk? FindChunk(ViteManifest manifest, string key)
	{
		if (manifest.TryGetValue(key, out var chunk)) return chunk;
		return manifest.FirstOrDefault(k =>
			k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(k.Value.Name, key, StringComparison.OrdinalIgnoreCase)).Value;
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
