namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Per-request render cache behind the IHtmlHelper extensions. One entry per
/// component+elementId, so a Svelte/SvelteHead pair shares a single render.
/// The Html side of an entry may only be consumed once: a second body render of
/// the same component with the same (implicit) elementId would silently reuse the
/// first render's output and duplicate its container div id, so it throws instead.
/// </summary>
internal static class SvelteRequestCache
{
	private sealed class Entry
	{
		public required SvelteRenderResult Result { get; init; }
		public bool HtmlConsumed { get; set; }
	}

	public static async Task<string> RenderHtmlAsync(HttpContext httpContext, string component, object? data, string? elementId)
	{
		var entry = await GetOrRenderAsync(httpContext, component, data, elementId).ConfigureAwait(false);
		return ConsumeHtml(entry, component);
	}

	public static async Task<string> RenderHeadAsync(HttpContext httpContext, string component, object? data, string? elementId)
	{
		return (await GetOrRenderAsync(httpContext, component, data, elementId).ConfigureAwait(false)).Result.Head;
	}

	public static string RenderHtml(HttpContext httpContext, string component, object? data, string? elementId)
	{
		return ConsumeHtml(GetOrRender(httpContext, component, data, elementId), component);
	}

	public static string RenderHead(HttpContext httpContext, string component, object? data, string? elementId)
	{
		return GetOrRender(httpContext, component, data, elementId).Result.Head;
	}

	private static string ConsumeHtml(Entry entry, string component)
	{
		if (entry.HtmlConsumed)
		{
			throw new InvalidOperationException(
				$"The Svelte component '{component}' was already rendered on this page. " +
				"To render the same component multiple times, pass a distinct elementId per instance — " +
				"the render cache and the container div id are keyed on it.");
		}

		entry.HtmlConsumed = true;
		return entry.Result.Html;
	}

	private static async Task<Entry> GetOrRenderAsync(HttpContext httpContext, string component, object? data, string? elementId)
	{
		var cacheKey = CacheKey(component, elementId);
		if (httpContext.Items[cacheKey] is Entry cached) return cached;

		var renderer = httpContext.RequestServices.GetRequiredService<SvelteRenderer>();
		var result = await renderer
			.RenderAsync(component, data, elementId, httpContext.RequestAborted)
			.ConfigureAwait(false);
		var entry = new Entry { Result = result };
		httpContext.Items[cacheKey] = entry;
		return entry;
	}

	private static Entry GetOrRender(HttpContext httpContext, string component, object? data, string? elementId)
	{
		var cacheKey = CacheKey(component, elementId);
		if (httpContext.Items[cacheKey] is Entry cached) return cached;

		var renderer = httpContext.RequestServices.GetRequiredService<SvelteRenderer>();
		var entry = new Entry { Result = renderer.Render(component, data, elementId) };
		httpContext.Items[cacheKey] = entry;
		return entry;
	}

	private static string CacheKey(string component, string? elementId) => $"sveltenet:{component}:{elementId}";
}
