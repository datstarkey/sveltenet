namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Renders Svelte components from MVC views (or any Razor view). With a
/// [SvelteComponent] model the component is resolved from the model type:
///     @await Html.SvelteAsync(Model)
///     @section Head { @await Html.SvelteHeadAsync(Model) }
/// or name the component explicitly:
///     @await Html.SvelteAsync("Components/Cart", Model.Cart)
///
/// Prefer the async overloads — like Html.PartialAsync, they are the general form. A
/// server render can await remote queries, so it has no correct synchronous equivalent;
/// the sync overloads work only for client-only rendering and otherwise throw, rather
/// than blocking a request thread on an awaiting render.
///
/// Results are cached per request so a Svelte/SvelteHead pair renders only once.
/// Rendering the same component multiple times on a page requires a distinct
/// elementId per instance — without one the second render throws, because the
/// cache and the container div id would silently collide.
/// </summary>
public static class SvelteHtmlHelperExtensions
{
	/// <summary>Renders the component bound to the model's [SvelteComponent] attribute.</summary>
	public static async Task<IHtmlContent> SvelteAsync(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(await SvelteRequestCache
			.RenderHtmlAsync(html.ViewContext.HttpContext, component, data, elementId).ConfigureAwait(false));
	}

	/// <summary>Head content for the component bound to the model's [SvelteComponent] attribute.</summary>
	public static async Task<IHtmlContent> SvelteHeadAsync(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(await SvelteRequestCache
			.RenderHeadAsync(html.ViewContext.HttpContext, component, data, elementId).ConfigureAwait(false));
	}

	public static async Task<IHtmlContent> SvelteAsync(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(await SvelteRequestCache
			.RenderHtmlAsync(html.ViewContext.HttpContext, component, data, elementId).ConfigureAwait(false));
	}

	public static async Task<IHtmlContent> SvelteHeadAsync(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(await SvelteRequestCache
			.RenderHeadAsync(html.ViewContext.HttpContext, component, data, elementId).ConfigureAwait(false));
	}

	/// <summary>Client-only render. Throws when SSR is active — use <see cref="SvelteAsync(IHtmlHelper, object, string?)"/>.</summary>
	public static IHtmlContent Svelte(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(SvelteRequestCache.RenderHtml(html.ViewContext.HttpContext, component, data, elementId));
	}

	/// <inheritdoc cref="Svelte(IHtmlHelper, object, string?)"/>
	public static IHtmlContent SvelteHead(this IHtmlHelper html, object data, string? elementId = null)
	{
		var component = ResolveComponent(html, data);
		return new HtmlString(SvelteRequestCache.RenderHead(html.ViewContext.HttpContext, component, data, elementId));
	}

	/// <inheritdoc cref="Svelte(IHtmlHelper, object, string?)"/>
	public static IHtmlContent Svelte(this IHtmlHelper html, string component, object? data = null, string? elementId = null)
	{
		return new HtmlString(SvelteRequestCache.RenderHtml(html.ViewContext.HttpContext, component, data, elementId));
	}

	/// <inheritdoc cref="Svelte(IHtmlHelper, object, string?)"/>
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
