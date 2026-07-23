namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.AspNetCore.Tests.Fixtures.Pages;
using System.Text.Json;

public class EnhancedResultTests
{
	private sealed class ExposedHomeModel : HomeModel
	{
		public IActionResult? Enhanced(IActionResult? result) => CreateEnhancedResult(result);
	}

	private sealed class NoopSsrEngine : ISvelteSsrEngine
	{
		public SsrResult Render(string componentModule, string renderModule, string? propsJson) => new();
	}

	private sealed class FakeUrlHelper : IUrlHelper
	{
		public ActionContext ActionContext { get; } = new()
		{
			HttpContext = new DefaultHttpContext(),
			RouteData = new RouteData { Values = { ["page"] = "/Home" } }
		};
		public string? Action(UrlActionContext actionContext) => null;
		public string? Content(string? contentPath) => contentPath;
		public bool IsLocalUrl(string? url) => true;
		public string? Link(string? routeName, object? values) => null;
		public string? RouteUrl(UrlRouteContext routeContext) => "/routed";
	}

	private static ExposedHomeModel CreatePage()
	{
		var services = new ServiceCollection()
			.AddSingleton(new SvelteOptions { IsDev = true })
			.AddSingleton<ISvelteSsrEngine, NoopSsrEngine>()
			.AddSingleton<SvelteRenderer>()
			.BuildServiceProvider();

		return new ExposedHomeModel
		{
			PageContext = new PageContext
			{
				HttpContext = new DefaultHttpContext { RequestServices = services },
				RouteData = new RouteData(),
				ActionDescriptor = new CompiledPageActionDescriptor { ViewEnginePath = "/Home" }
			},
			Url = new FakeUrlHelper()
		};
	}

	private static string Serialize(IActionResult? result) =>
		JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, SvelteRenderer.JsonOptions);

	[Fact]
	public void Page_render_becomes_the_fresh_props_bag()
	{
		var page = CreatePage();
		page.Title = "fresh";
		page.ModelState.AddModelError("NewLabel", "required");

		var json = Serialize(page.Enhanced(new PageResult()));

		Assert.Contains("\"data\":", json);
		Assert.Contains("\"title\":\"fresh\"", json);
		Assert.Contains("\"newLabel\":[\"required\"]", json);
	}

	[Fact]
	public void Redirect_to_page_becomes_a_redirect_payload()
	{
		var json = Serialize(CreatePage().Enhanced(new RedirectToPageResult(null)));

		Assert.Equal("{\"redirect\":\"/routed\"}", json);
	}

	[Fact]
	public void Plain_redirects_pass_their_url_through()
	{
		var page = CreatePage();

		Assert.Equal("{\"redirect\":\"/elsewhere\"}", Serialize(page.Enhanced(new RedirectResult("/elsewhere"))));
		Assert.Equal("{\"redirect\":\"/local\"}", Serialize(page.Enhanced(new LocalRedirectResult("/local"))));
	}

	[Fact]
	public void Other_results_are_left_untouched()
	{
		Assert.Null(CreatePage().Enhanced(new ContentResult()));
		Assert.Null(CreatePage().Enhanced(new StatusCodeResult(404)));
		Assert.Null(CreatePage().Enhanced(null));
	}
}
