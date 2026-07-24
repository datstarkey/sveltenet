namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.AspNetCore.Tests.Fixtures;
using SvelteNet.AspNetCore.Tests.Fixtures.Pages;
using SvelteNet.AspNetCore.Tests.Fixtures.Pages.Admin;

public class SveltePageTests
{
	private sealed class NoopSsrEngine : ISvelteSsrEngine
	{
		public SsrResult Render(string componentModule, string renderModule, string? propsJson) => new();
	}

	private static T CreatePage<T>(string viewEnginePath) where T : SveltePage, new()
	{
		var services = new ServiceCollection()
			.AddSingleton(new SvelteOptions { IsDev = true })
			.AddSingleton<ISvelteSsrEngine, NoopSsrEngine>()
			.AddSingleton<SvelteRenderer>()
			.BuildServiceProvider();

		return new T
		{
			PageContext = new PageContext
			{
				HttpContext = new DefaultHttpContext { RequestServices = services },
				RouteData = new RouteData(),
				ActionDescriptor = new CompiledPageActionDescriptor { ViewEnginePath = viewEnginePath }
			}
		};
	}

	[Fact]
	public void Component_path_derives_from_the_page_route()
	{
		var page = CreatePage<UsersModel>("/Admin/Users");

		var html = page.Svelte().ToString()!;

		Assert.Contains("Svelte/Admin/Users.svelte", html);
		Assert.Contains("<div id=\"svelte-admin-users\">", html);
	}

	[Fact]
	public void Svelte_props_are_serialized_into_the_data_prop()
	{
		var page = CreatePage<HomeModel>("/Home");
		page.Title = "Hello";
		page.Widgets = [new Widget { Name = "w1", Kind = WidgetKind.Fancy }];

		var html = page.Svelte().ToString()!;

		Assert.Contains("\"title\":\"Hello\"", html);
		Assert.Contains("\"widgets\":[{\"name\":\"w1\",\"kind\":\"fancy\"}]", html);
	}

	[Fact]
	public void Model_state_errors_are_passed_as_a_validation_problem_with_camel_cased_keys()
	{
		var page = CreatePage<HomeModel>("/Home");
		page.ModelState.AddModelError("NewLabel", "A label is required.");

		var html = page.Svelte().ToString()!;

		Assert.Contains("\"problem\":{", html);
		Assert.Contains("\"errors\":{\"newLabel\":[\"A label is required.\"]}", html);
	}

	[Fact]
	public void Problem_is_null_when_model_state_is_valid()
	{
		var page = CreatePage<HomeModel>("/Home");

		var html = page.Svelte().ToString()!;

		Assert.Contains("\"problem\":null", html);
	}

	[Fact]
	public void Antiforgery_token_is_empty_when_the_service_is_absent()
	{
		var page = CreatePage<HomeModel>("/Home");

		var html = page.Svelte().ToString()!;

		Assert.Contains("\"antiforgeryToken\":\"\"", html);
	}

	[Fact]
	public void Render_result_is_cached_per_request()
	{
		var page = CreatePage<HomeModel>("/Home");

		var first = page.Svelte().ToString();
		page.Title = "changed after first render";
		var second = page.Svelte().ToString();

		Assert.Equal(first, second);
	}
}
