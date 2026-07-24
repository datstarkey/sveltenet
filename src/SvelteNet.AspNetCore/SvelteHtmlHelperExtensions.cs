namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Renders Svelte components from MVC views (or any Razor view). With a
/// [SvelteComponent] model the component is resolved from the model type:
///     @Html.Svelte(Model)
///     @section Head { @Html.SvelteHead(Model) }
/// or name the component explicitly:
///     @Html.Svelte("Components/Cart", Model.Cart)
/// Results are cached per request so a Svelte/SvelteHead pair renders only once.
/// Rendering the same component multiple times on a page requires a distinct
/// elementId per instance — without one the second render throws, because the
/// cache and the container div id would silently collide.
/// </summary>
public static class SvelteHtmlHelperExtensions
{
	/// <summary>Renders the component bound to the model's [SvelteComponent] attribute.</summary>
	public static IHtmlContent Svelte(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(SvelteRequestCache.RenderHtml(html.ViewContext.HttpContext, component, data, elementId));
	}

	/// <summary>Head content for the component bound to the model's [SvelteComponent] attribute.</summary>
	public static IHtmlContent SvelteHead(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(SvelteRequestCache.RenderHead(html.ViewContext.HttpContext, component, data, elementId));
	}

	public static IHtmlContent Svelte(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(SvelteRequestCache.RenderHtml(html.ViewContext.HttpContext, component, data, elementId));
	}

	public static IHtmlContent SvelteHead(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(SvelteRequestCache.RenderHead(html.ViewContext.HttpContext, component, data, elementId));
	}

	private static string ResolveComponent(IHtmlHelper html, object data)
	{
		var options = html.ViewContext.HttpContext.RequestServices.GetRequiredService<SvelteOptions>();
		return SvelteComponentResolver.Resolve(data.GetType(), options.EnableReflectionFallback);
	}
}
