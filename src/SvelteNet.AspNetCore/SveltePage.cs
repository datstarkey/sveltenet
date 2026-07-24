namespace SvelteNet.AspNetCore;

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Generated;
using SvelteNet.TypeGen;

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
		SvelteGeneratedMetadata.TryGetPage(GetType(), out var generated)
			? generated.Component
			: PageContext.ActionDescriptor.ViewEnginePath.TrimStart('/');

	protected virtual ComponentOptions SvelteComponentOptions()
	{
		Dictionary<string, object?> data;
		if (SvelteGeneratedMetadata.TryGetPage(GetType(), out var generated))
		{
			data = generated.Properties.ToDictionary(p => p.JsonName, p => p.GetValue(this));
		}
		else
		{
			var options = HttpContext.RequestServices.GetRequiredService<SvelteOptions>();
			if (!options.EnableReflectionFallback)
				throw new InvalidOperationException(
					$"No generated Svelte page descriptor was found for '{GetType().FullName}'. " +
					"Reference SvelteNet.Generators as an analyzer, or explicitly enable the legacy reflection fallback.");
			var props = PropCache.GetOrAdd(GetType(), static t => t
				.GetProperties()
				.Where(p => p.IsDefined(typeof(SveltePropAttribute), true))
				.ToArray());
			data = props.ToDictionary(p => p.Name.ToCamelCase(), object? (p) => p.GetValue(this));
		}

		data["problem"] = ValidationProblem();
		data["antiforgeryToken"] = HttpContext.RequestServices
			.GetService<IAntiforgery>()?.GetAndStoreTokens(HttpContext).RequestToken ?? string.Empty;

		return new ComponentOptions
		{
			Component = SvelteComponent,
			Props = new Dictionary<string, object?> { ["data"] = data },
			CancellationToken = HttpContext.RequestAborted
		};
	}

	private SvelteRenderResult Rendered => _rendered ??= SvelteRenderer.Render(SvelteComponentOptions());

	public IHtmlContent Svelte() => new HtmlString(Rendered.Html);

	public IHtmlContent SvelteHead() => new HtmlString(Rendered.Head);

	/// <summary>True when the request came from the enhance() helper and expects JSON data.</summary>
	protected bool IsEnhancedRequest => Request.Headers.ContainsKey("X-SvelteNet");

	/// <summary>
	/// ValidationProblemDetails-shaped view of ModelState (RFC 9457 `errors` member),
	/// or null when the model state is valid. Rendered into the data prop as `problem`
	/// so SSR re-renders (no-JS posts) and enhanced responses share one errors shape.
	/// </summary>
	private object? ValidationProblem()
	{
		if (ModelState.IsValid) return null;
		return new
		{
			Title = "One or more validation errors occurred.",
			Status = StatusCodes.Status400BadRequest,
			Errors = ModelState
				.Where(kv => kv.Value?.Errors.Count > 0)
				.ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
		};
	}

	/// <summary>
	/// Answers enhanced requests with JSON instead of HTML: a page render becomes the
	/// fresh props bag, a redirect (post/redirect/get) becomes a payload the client
	/// follows with a second enhanced fetch, and validation failures become an RFC 9457
	/// problem details response (400, application/problem+json) whose `data` extension
	/// member carries the fresh props so the island still re-renders.
	/// </summary>
	protected virtual IActionResult? CreateEnhancedResult(IActionResult? result) => result switch
	{
		PageResult when !ModelState.IsValid => new JsonResult(
			new
			{
				Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
				Title = "One or more validation errors occurred.",
				Status = StatusCodes.Status400BadRequest,
				Errors = ModelState
					.Where(kv => kv.Value?.Errors.Count > 0)
					.ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()),
				Data = ((Dictionary<string, object?>)SvelteComponentOptions().Props!)["data"]
			},
			SvelteRenderer.JsonOptions)
		{
			StatusCode = StatusCodes.Status400BadRequest,
			ContentType = "application/problem+json"
		},
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
