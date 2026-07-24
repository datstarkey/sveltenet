namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public class SvelteRequestCacheTests
{
	private sealed class NoopSsrEngine : ISvelteSsrEngine
	{
		public SsrResult Render(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default) => new();
	}

	private static HttpContext CreateHttpContext()
	{
		var services = new ServiceCollection()
			.AddSingleton(new SvelteOptions { IsDev = true })
			.AddSingleton<ISvelteSsrEngine, NoopSsrEngine>()
			.AddSingleton<SvelteRenderer>()
			.BuildServiceProvider();

		return new DefaultHttpContext { RequestServices = services };
	}

	[Fact]
	public void A_svelte_and_sveltehead_pair_shares_one_cached_render()
	{
		var httpContext = CreateHttpContext();

		var html = SvelteRequestCache.RenderHtml(httpContext, "Components/Card", new { Name = "a" }, null);
		var head = SvelteRequestCache.RenderHead(httpContext, "Components/Card", null, null);

		Assert.Contains("svelte-components-card", html);
		Assert.Equal(string.Empty, head);
	}

	[Fact]
	public void Rendering_the_same_component_twice_without_element_ids_throws()
	{
		var httpContext = CreateHttpContext();
		SvelteRequestCache.RenderHtml(httpContext, "Components/Card", new { Name = "a" }, null);

		var ex = Assert.Throws<InvalidOperationException>(() =>
			SvelteRequestCache.RenderHtml(httpContext, "Components/Card", new { Name = "b" }, null));

		Assert.Contains("elementId", ex.Message);
		Assert.Contains("Components/Card", ex.Message);
	}

	[Fact]
	public void Distinct_element_ids_allow_multiple_instances()
	{
		var httpContext = CreateHttpContext();

		var first = SvelteRequestCache.RenderHtml(httpContext, "Components/Card", new { Name = "a" }, "card-a");
		var second = SvelteRequestCache.RenderHtml(httpContext, "Components/Card", new { Name = "b" }, "card-b");

		Assert.Contains("id=\"card-a\"", first);
		Assert.Contains("\"name\":\"a\"", first);
		Assert.Contains("id=\"card-b\"", second);
		Assert.Contains("\"name\":\"b\"", second);
	}

	[Fact]
	public void Head_can_be_read_any_number_of_times()
	{
		var httpContext = CreateHttpContext();
		SvelteRequestCache.RenderHtml(httpContext, "Components/Card", null, null);

		SvelteRequestCache.RenderHead(httpContext, "Components/Card", null, null);
		SvelteRequestCache.RenderHead(httpContext, "Components/Card", null, null);
	}
}
