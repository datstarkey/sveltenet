namespace Svelte.Net.Core
{
	using Extensions;
	using Jint;
	using Models;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	public class SvelteService
	{
		private static readonly Dictionary<RenderType, ViteManifest> Manifests = new();
		private readonly SvelteOptions _options;

		public SvelteService(SvelteOptions options)
		{
			_options = options;
		}

		private string ConvertRoute(string value)
		{
			var end = value.Split('/').Last();
			var str = value.Replace(end, end.ToCamelCase());
			return str;
		}

		private string RouteToComponent(string? route)
		{
			return route?.TrimStart('/') + ".svelte";
		}

		public string GetHtmlString(string? path, object? data)
		{
			return GetHtmlString(new ComponentOptions()
			{
				Path = path,
				Data = data,
				Csr = _options.EnableCsr,
				Ssr = _options.EnableSsr,
				PagesPath = _options.PagesPath
			});
		}

		public string GetHtmlString(string? path)
		{
			return GetHtmlString(new ComponentOptions()
			{
				Path = path,
				Csr = _options.EnableCsr,
				Ssr = _options.EnableSsr,
				PagesPath = _options.PagesPath
			});
		}

		public string GetHtmlString(ComponentOptions componentOptions)
		{
			var componentRoute = $"/{componentOptions.PagesPath}/" + RouteToComponent(componentOptions.Path);
			componentOptions.Id ??= componentRoute.ToLower().Replace(".svelte", "");
			var serializedData = componentOptions.Data == null ? string.Empty : JsonSerializer.Serialize(componentOptions.Data, JsonOptions);

			var str = "<div id='" + componentOptions.Id + "'>";
			if (componentOptions.Ssr) str += GetSsr(componentRoute, serializedData).Html;
			str += "</div>";
			if (componentOptions.Csr) str += GetCsr(componentRoute, serializedData, componentOptions.Id);
			return str;
		}

		public string GetCss(string? route)
		{
			if (_options.IsDev) return "";
			var manifestRoute = GetRoute(route, RenderType.Csr);
			return manifestRoute?.Css == null ? string.Empty : string.Join(Environment.NewLine, manifestRoute.Css.Select(x => $"<link rel='stylesheet' href='{x}' />"));
		}

		private string GetCsr(string? route, string? serializedData = null, string? elementId = null)
		{
			var dataStr = string.IsNullOrEmpty(serializedData) ?
				string.Empty :
				$", \n      props: {{data: {serializedData}}}";

			elementId ??= route?.ToLower().Replace(".svelte", "");

			var src = _options.IsDev ?
				$"{_options.DevServer}{route}" :
				$"/client/{GetRoute(route, RenderType.Csr)?.File}";

			return @$"

<script id='{elementId}-script' type='module'>
  import App from '{src}';
  new App({{
	  target: document.getElementById('{elementId}'),
	  hydrate: true{dataStr}
  }})
  document.getElementById('{elementId}-script')?.remove();
</script>";
		}
		private SsrModel GetSsr(string? route, string? serializedData = null)
		{
			var result = new SsrModel();
			var dataStr = string.IsNullOrEmpty(serializedData) ? string.Empty : $"{{ data: {serializedData} }}";
			if (!_options.IsDev)
			{
				var manifestRoute = GetRoute(route, RenderType.Ssr);
				if (manifestRoute?.File == null) return result;
				var serverDir = Path.Combine(Directory.GetCurrentDirectory(), _options.ServerLocation);
				var engine = new Engine(o => o.EnableModules(serverDir));
				engine.SetValue("app", engine.ImportModule("./" + manifestRoute.File).Get("default"));
				engine.SetValue("result", result);
				engine.SetValue("console", new ConsoleWrapper());
				engine.Execute(@$"
function render(){{
	const {{html, head}} = app.render({dataStr})
	result.Html = html;
	result.Head = head;
}}");
				engine.Invoke("render");

			}
			result.Html = $@"{result.Html}";
			return result;
		}

		public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			}
		};

		private ViteRoute? GetRoute(string? route, RenderType type)
		{
			if (route == null) return null;
			var manifest = GetManifest(type);
			return manifest.FirstOrDefault(k => k.Key.Equals(route.TrimStart('/'), StringComparison.OrdinalIgnoreCase)).Value;
		}

		private ViteManifest GetManifest(RenderType type)
		{
			var routeType = type switch
			{
				RenderType.Ssr => _options.ServerLocation,
				RenderType.Csr => _options.ClientLocation,
				_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
			};

			if (Manifests.TryGetValue(type, out var result))
			{
				return result;
			}
			var file = Path.Combine(Directory.GetCurrentDirectory(), routeType, "manifest.json");
			var text = File.ReadAllText(file);
			var manifest = JsonSerializer.Deserialize<ViteManifest>(text);
			if (manifest != null && !Manifests.ContainsKey(type))
				Manifests.Add(type, manifest);
			return manifest ?? new ViteManifest();
		}
	}
}
