namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.TypeGen;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Razor Pages base model that renders the Svelte component matching the page's route,
/// passing all [SvelteProp] properties (plus model state errors) as the "data" prop.
///
/// In the .cshtml:
///     @Model.Svelte()               — container + SSR html + hydration script
///     @Model.SvelteHead()           — SSR head + stylesheet links (place in the head section)
/// </summary>
public abstract class SveltePage : PageModel
{
	private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropCache = new();
	private SvelteRenderResult? _rendered;

	protected SvelteRenderer SvelteRenderer =>
		HttpContext.RequestServices.GetRequiredService<SvelteRenderer>();

	/// <summary>Component path derived from the page route, e.g. "Index" or "Admin/Users".</summary>
	public virtual string SvelteComponent =>
		PageContext.ActionDescriptor.ViewEnginePath.TrimStart('/');

	protected virtual ComponentOptions SvelteComponentOptions()
	{
		var props = PropCache.GetOrAdd(GetType(), static t => t
			.GetProperties()
			.Where(p => p.IsDefined(typeof(SveltePropAttribute), true))
			.ToArray());

		var data = props.ToDictionary(p => p.Name.ToCamelCase(), object? (p) => p.GetValue(this));
		data["modelState"] = ModelState
			.Where(kv => kv.Value?.Errors.Count > 0)
			.ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
		data["antiforgeryToken"] = HttpContext.RequestServices
			.GetService<IAntiforgery>()?.GetAndStoreTokens(HttpContext).RequestToken ?? string.Empty;

		return new ComponentOptions
		{
			Component = SvelteComponent,
			Props = new Dictionary<string, object?> { ["data"] = data }
		};
	}

	private SvelteRenderResult Rendered => _rendered ??= SvelteRenderer.Render(SvelteComponentOptions());

	public IHtmlContent Svelte() => new HtmlString(Rendered.Html);

	public IHtmlContent SvelteHead() => new HtmlString(Rendered.Head);

	/// <summary>True when the request came from the enhance() helper and expects JSON data.</summary>
	protected bool IsEnhancedRequest => Request.Headers.ContainsKey("X-SvelteNet");

	/// <summary>
	/// Answers enhanced requests with JSON instead of HTML: a page render becomes the
	/// fresh props bag (so validation errors round-trip through modelState), and a
	/// redirect (post/redirect/get) becomes a payload the client follows with a
	/// second enhanced fetch. Anything else is left untouched.
	/// </summary>
	protected virtual IActionResult? CreateEnhancedResult(IActionResult? result) => result switch
	{
		PageResult => new JsonResult(SvelteComponentOptions().Props, SvelteRenderer.JsonOptions),
		RedirectToPageResult r => new JsonResult(
			new { redirect = Url.Page(r.PageName, r.PageHandler, r.RouteValues) ?? Request.Path.Value },
			SvelteRenderer.JsonOptions),
		LocalRedirectResult r => new JsonResult(new { redirect = r.Url }, SvelteRenderer.JsonOptions),
		RedirectResult r => new JsonResult(new { redirect = r.Url }, SvelteRenderer.JsonOptions),
		_ => null
	};

	public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
	{
		OnPageHandlerExecuting(context);
		if (context.Result != null) return;

		var executed = await next();
		OnPageHandlerExecuted(executed);

		if (!IsEnhancedRequest) return;
		var replacement = CreateEnhancedResult(executed.Result);
		if (replacement != null) executed.Result = replacement;
	}
}
