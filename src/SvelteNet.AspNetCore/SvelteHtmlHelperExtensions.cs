namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

/// <summary>
/// Renders Svelte components from MVC views (or any Razor view) with an explicit
/// component path and data model:
///     @Html.Svelte("Components/Cart", Model.Cart)
///     @Html.SvelteHead("Components/Cart")
/// Results are cached per request so a Svelte/SvelteHead pair renders only once.
/// Rendering the same component multiple times on a page requires a distinct
/// elementId per instance — without one the second render throws, because the
/// cache and the container div id would silently collide.
/// </summary>
public static class SvelteHtmlHelperExtensions
{
	public static IHtmlContent Svelte(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(SvelteRequestCache.RenderHtml(html.ViewContext.HttpContext, component, data, elementId));
	}

	public static IHtmlContent SvelteHead(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(SvelteRequestCache.RenderHead(html.ViewContext.HttpContext, component, data, elementId));
	}
}
