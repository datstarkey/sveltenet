namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

/// <summary>
/// Full-stack tests of the enhance() HTTP protocol against the real TodoApp sample:
/// routing, model binding, antiforgery, and the JSON rewriting in SveltePage.
/// Runs in dev mode (no Vite build required) with scaffolding disabled so the
/// sample's source tree is never touched.
/// </summary>
public class EnhanceIntegrationTests : IClassFixture<EnhanceIntegrationTests.TodoAppFactory>
{
	public class TodoAppFactory : WebApplicationFactory<Program>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseEnvironment("Development");
			builder.ConfigureTestServices(services =>
				services.AddSingleton(new SvelteOptions { IsDev = true, EnableScaffolding = false }));
		}
	}

	private readonly TodoAppFactory _factory;

	public EnhanceIntegrationTests(TodoAppFactory factory)
	{
		_factory = factory;
	}

	private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
	{
		AllowAutoRedirect = false
	});

	private static HttpRequestMessage Enhanced(HttpMethod method, string url) =>
		new(method, url) { Headers = { { "X-SvelteNet", "true" } } };

	private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
	{
		response.EnsureSuccessStatusCode();
		Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
		return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
	}

	private static async Task<(JsonElement Data, string Token)> GetData(HttpClient client)
	{
		var json = await ReadJson(await client.SendAsync(Enhanced(HttpMethod.Get, "/")));
		var data = json.GetProperty("data");
		return (data, data.GetProperty("antiforgeryToken").GetString()!);
	}

	private static HttpRequestMessage EnhancedPost(string url, string token, params (string Key, string Value)[] fields)
	{
		var request = Enhanced(HttpMethod.Post, url);
		request.Content = new FormUrlEncodedContent(
			fields.Append(("__RequestVerificationToken", token))
				.Select(((string Key, string Value) f) => new KeyValuePair<string, string>(f.Key, f.Value)));
		return request;
	}

	[Fact]
	public async Task Enhanced_get_returns_the_data_prop_as_json()
	{
		var (data, token) = await GetData(CreateClient());

		Assert.Equal("SvelteNet Todos", data.GetProperty("title").GetString());
		Assert.True(data.GetProperty("todos").GetArrayLength() > 0);
		Assert.NotEmpty(token);
		Assert.Equal(JsonValueKind.Object, data.GetProperty("modelState").ValueKind);
	}

	[Fact]
	public async Task Enhanced_post_follows_post_redirect_get_as_json()
	{
		var client = CreateClient();
		var (_, token) = await GetData(client);

		var post = await ReadJson(await client.SendAsync(
			EnhancedPost("/", token, ("NewLabel", "integration-added"), ("NewPriority", "High"))));

		Assert.Equal("/", post.GetProperty("redirect").GetString());

		var (data, _) = await GetData(client);
		var labels = data.GetProperty("todos").EnumerateArray()
			.Select(t => t.GetProperty("label").GetString());
		Assert.Contains("integration-added", labels);
	}

	[Fact]
	public async Task Enhanced_post_with_validation_errors_returns_model_state()
	{
		var client = CreateClient();
		var (_, token) = await GetData(client);

		var json = await ReadJson(await client.SendAsync(
			EnhancedPost("/", token, ("NewLabel", ""), ("NewPriority", "Low"))));

		Assert.False(json.TryGetProperty("redirect", out _));
		var errors = json.GetProperty("data").GetProperty("modelState").GetProperty("newLabel");
		Assert.Equal("A label is required.", errors[0].GetString());
	}

	[Fact]
	public async Task Enhanced_post_to_a_named_handler_works()
	{
		var client = CreateClient();
		var (before, token) = await GetData(client);
		var first = before.GetProperty("todos")[0];
		var id = first.GetProperty("id").GetInt32();
		var wasDone = first.GetProperty("done").GetBoolean();

		var post = await ReadJson(await client.SendAsync(
			EnhancedPost("/?handler=toggle", token, ("id", id.ToString()))));
		Assert.Equal("/", post.GetProperty("redirect").GetString());

		var (after, _) = await GetData(client);
		var toggled = after.GetProperty("todos").EnumerateArray()
			.First(t => t.GetProperty("id").GetInt32() == id);
		Assert.Equal(!wasDone, toggled.GetProperty("done").GetBoolean());
	}

	[Fact]
	public async Task Plain_post_still_redirects_with_html_semantics()
	{
		var client = CreateClient();
		var (_, token) = await GetData(client);

		var request = new HttpRequestMessage(HttpMethod.Post, "/")
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["NewLabel"] = "plain-post",
				["NewPriority"] = "Low",
				["__RequestVerificationToken"] = token
			})
		};

		var response = await client.SendAsync(request);

		Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
	}

	[Fact]
	public async Task Plain_get_still_renders_html()
	{
		var response = await CreateClient().GetAsync("/");

		response.EnsureSuccessStatusCode();
		var html = await response.Content.ReadAsStringAsync();
		Assert.Contains("<div id=\"svelte-index\">", html);
		Assert.Contains("mountComponent", html);
	}

	[Fact]
	public async Task Attributed_view_models_render_via_the_typed_helper()
	{
		var response = await CreateClient().GetAsync("/Hello");

		response.EnsureSuccessStatusCode();
		var html = await response.Content.ReadAsStringAsync();
		Assert.Contains("<div id=\"svelte-components-hello\">", html);
		Assert.Contains("Svelte/Components/Hello.svelte", html);
		Assert.Contains("\"visits\":", html);
	}

	[Fact]
	public async Task Post_without_antiforgery_token_is_rejected()
	{
		var client = CreateClient();
		var request = Enhanced(HttpMethod.Post, "/");
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["NewLabel"] = "no-token",
			["NewPriority"] = "Low"
		});

		var response = await client.SendAsync(request);

		Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
	}
}
