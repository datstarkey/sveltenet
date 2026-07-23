namespace SvelteNet.AspNetCore.Tests;

using System.Net;

/// <summary>
/// Integration tests of the MVC hosting path against the MvcHello sample:
/// [SvelteComponent] resolution and @Html.Svelte(Model) rendering.
/// </summary>
public class MvcIntegrationTests : IClassFixture<MvcHelloFactory>
{
	private readonly MvcHelloFactory _factory;

	public MvcIntegrationTests(MvcHelloFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task The_controller_renders_a_svelte_island_with_serialized_props()
	{
		var response = await _factory.CreateClient().GetAsync("/");
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		// [SvelteComponent] resolves HelloViewModel → Components/Hello.svelte
		Assert.Contains("<div id=\"svelte-components-hello\">", html);
		Assert.Contains("Svelte/Components/Hello.svelte", html);
		// props serialize through SvelteJson: camelCase, wrapped in { data: ... }
		Assert.Contains("\"name\":\"SvelteNet\"", html);
		Assert.Contains("\"visits\":", html);
	}
}
