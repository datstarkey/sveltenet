namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
		public ValueTask<SsrResult> RenderAsync(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default) => ValueTask.FromResult(new SsrResult());
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
	public async Task Component_path_derives_from_the_page_route()
	{
		var page = CreatePage<UsersModel>("/Admin/Users");

		var html = (await page.SvelteAsync()).ToString()!;

		Assert.Contains("Svelte/Admin/Users.svelte", html);
		Assert.Contains("<div id=\"svelte-admin-users\">", html);
	}

	[Fact]
	public async Task Svelte_props_are_serialized_into_the_data_prop()
	{
		var page = CreatePage<HomeModel>("/Home");
		page.Title = "Hello";
		page.Widgets = [new Widget { Name = "w1", Kind = WidgetKind.Fancy }];

		var html = (await page.SvelteAsync()).ToString()!;

		Assert.Contains("\"title\":\"Hello\"", html);
		Assert.Contains("\"widgets\":[{\"name\":\"w1\",\"kind\":\"fancy\"}]", html);
	}

	[Fact]
	public async Task Model_state_errors_are_passed_as_a_validation_problem_with_camel_cased_keys()
	{
		var page = CreatePage<HomeModel>("/Home");
		page.ModelState.AddModelError("NewLabel", "A label is required.");

		var html = (await page.SvelteAsync()).ToString()!;

		Assert.Contains("\"problem\":{", html);
		Assert.Contains("\"errors\":{\"newLabel\":[\"A label is required.\"]}", html);
	}

	[Fact]
	public async Task Problem_is_null_when_model_state_is_valid()
	{
		var page = CreatePage<HomeModel>("/Home");

		var html = (await page.SvelteAsync()).ToString()!;

		Assert.Contains("\"problem\":null", html);
	}

	[Fact]
	public async Task Antiforgery_token_is_empty_when_the_service_is_absent()
	{
		var page = CreatePage<HomeModel>("/Home");

		var html = (await page.SvelteAsync()).ToString()!;

		Assert.Contains("\"antiforgeryToken\":\"\"", html);
	}

	[Fact]
	public async Task Render_result_is_cached_per_request()
	{
		var page = CreatePage<HomeModel>("/Home");

		var first = (await page.SvelteAsync()).ToString();
		page.Title = "changed after first render";
		var second = (await page.SvelteAsync()).ToString();

		Assert.Equal(first, second);
	}

	/// <summary>
	/// The page filter renders before the view runs, so the synchronous accessors a .cshtml
	/// uses (@Model.Svelte()) find a finished result and never block on SSR.
	/// </summary>
	[Fact]
	public async Task The_page_filter_prerenders_so_the_synchronous_accessors_work()
	{
		var page = CreatePage<HomeModel>("/Home");
		page.Title = "Hello";

		await RunPageFilter(page, new PageResult());

		Assert.Contains("\"title\":\"Hello\"", page.Svelte().ToString()!);
		Assert.NotNull(page.SvelteHead());
	}

	[Fact]
	public void Synchronous_accessors_explain_themselves_when_the_filter_did_not_run()
	{
		var page = CreatePage<HomeModel>("/Home");

		var ex = Assert.Throws<InvalidOperationException>(() => page.Svelte());

		Assert.Contains("SvelteAsync", ex.Message);
	}

	/// <summary>A redirect result never reaches a view, so there is nothing to prerender.</summary>
	[Fact]
	public async Task The_page_filter_skips_rendering_for_non_page_results()
	{
		var page = CreatePage<HomeModel>("/Home");

		await RunPageFilter(page, new RedirectResult("/elsewhere"));

		Assert.Throws<InvalidOperationException>(() => page.Svelte());
	}

	private static async Task RunPageFilter(SveltePage page, IActionResult result)
	{
		var actionDescriptor = page.PageContext.ActionDescriptor;
		var executing = new PageHandlerExecutingContext(
			page.PageContext,
			[],
			handlerMethod: null,
			new Dictionary<string, object?>(),
			page);
		var executed = new PageHandlerExecutedContext(
			page.PageContext,
			[],
			handlerMethod: null,
			page)
		{ Result = result };

		await page.OnPageHandlerExecutionAsync(executing, () => Task.FromResult(executed));
		_ = actionDescriptor;
	}
}
