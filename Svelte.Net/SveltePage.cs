namespace Svelte.Net;

using Core;
using Core.Attributes;
using Core.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;

public abstract class SveltePage : PageModel
{
	protected readonly SvelteService SvelteService;
	protected readonly SvelteOptions SvelteOptions;
	public string? Route => Url.ActionContext.ActionDescriptor.DisplayName;

	protected SveltePage()
	{
		SvelteService = SvelteNet.CreateService();
		SvelteOptions = SvelteNet.Options.Clone();
	}

	protected ComponentOptions SvelteComponentOptions()
	{
		var props = GetType().GetProperties()
			.Where(p => p.GetCustomAttributes(typeof(SvelteBindAttribute), true).Any()).ToList();

		var options = new ComponentOptions()
		{
			PagesPath = SvelteOptions.PagesPath,
			Csr = SvelteOptions.EnableCsr,
			Ssr = SvelteOptions.EnableSsr,
			Path = Route
		};

		if (!props.Any()) return options;

		var data = props.ToDictionary(property => property.Name.ToCamelCase(), property => property.GetValue(this));
		data.Add("modelState", ModelState);
		options.Data = data;
		return options;
	}

	public virtual Task<IHtmlContent> Svelte()
	{
		var result = new HtmlString(SvelteService.GetHtmlString(SvelteComponentOptions()));
		return Task.FromResult(result as IHtmlContent);
	}

	public virtual IHtmlContent StyleContent()
	{
		return new HtmlString(SvelteService.GetCss(Route));
	}
}
