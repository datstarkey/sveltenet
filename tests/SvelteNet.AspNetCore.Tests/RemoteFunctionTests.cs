namespace SvelteNet.AspNetCore.Tests;

using Microsoft.Extensions.DependencyInjection;
using SvelteNet.AspNetCore.Remote;
using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>
/// Integration tests of the query/command/form remote protocol against the RemoteFunctions
/// sample's [SvelteRemote] TodoApi, dispatched through the source-generated descriptors.
/// </summary>
public class RemoteFunctionTests : IClassFixture<RemoteFunctionsFactory>
{
	private readonly RemoteFunctionsFactory _factory;

	public RemoteFunctionTests(RemoteFunctionsFactory factory)
	{
		_factory = factory;
	}

	private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
	{
		response.EnsureSuccessStatusCode();
		return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
	}

	private static HttpRequestMessage Command(string path, object args, bool withHeader = true)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, $"/_sveltenet/remote/{path}")
		{
			Content = new StringContent(JsonSerializer.Serialize(args, SvelteJson.Options), Encoding.UTF8, "application/json")
		};
		if (withHeader) request.Headers.Add("X-SvelteNet", "true");
		return request;
	}

	private static HttpRequestMessage Form(string path, Dictionary<string, string> fields, bool enhanced = true, bool validateOnly = false)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, $"/_sveltenet/remote/{path}")
		{
			Content = new FormUrlEncodedContent(fields)
		};
		if (enhanced) request.Headers.Add("X-SvelteNet", "true");
		if (validateOnly) request.Headers.Add("X-SvelteNet-Validate", "true");
		return request;
	}

	[Fact]
	public void The_sample_service_dispatches_through_generated_descriptors()
	{
		var registry = _factory.Services.GetRequiredService<SvelteRemoteRegistry>();

		var descriptor = Assert.Single(registry.Services, s => s.Name == "TodoApi");
		Assert.True(descriptor.IsGenerated);
		Assert.Equal(5, descriptor.Methods.Length);
	}

	[Fact]
	public async Task Queries_are_served_over_get()
	{
		var json = await ReadJson(await _factory.CreateClient().GetAsync("/_sveltenet/remote/TodoApi/GetStats"));

		Assert.True(json.GetProperty("total").GetInt32() > 0);
		Assert.True(json.TryGetProperty("byPriority", out _));
	}

	[Fact]
	public async Task Queries_reject_other_kinds_and_posts()
	{
		var client = _factory.CreateClient();

		// a command over GET
		Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/_sveltenet/remote/TodoApi/ToggleTodo")).StatusCode);
		// a query over POST
		Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Command("TodoApi/GetStats", new { }))).StatusCode);
		// unknown method
		Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/_sveltenet/remote/TodoApi/Nope")).StatusCode);
	}

	[Fact]
	public async Task Commands_bind_json_arguments_and_return_204_for_void()
	{
		var client = _factory.CreateClient();
		var todos = await ReadJson(await client.GetAsync("/_sveltenet/remote/TodoApi/GetTodos"));
		var id = todos[0].GetProperty("id").GetInt32();

		var response = await client.SendAsync(Command("TodoApi/ToggleTodo", new { id }));

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
	}

	[Fact]
	public async Task Commands_require_the_header_and_report_binding_issues()
	{
		var client = _factory.CreateClient();

		var noHeader = await client.SendAsync(Command("TodoApi/ToggleTodo", new { id = 1 }, withHeader: false));
		Assert.Equal(HttpStatusCode.BadRequest, noHeader.StatusCode);

		var missingArg = await client.SendAsync(Command("TodoApi/ToggleTodo", new { }));
		Assert.Equal(HttpStatusCode.BadRequest, missingArg.StatusCode);
		Assert.Contains("Missing argument 'id'", await missingArg.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Forms_bind_fields_run_async_handlers_and_return_the_result()
	{
		var json = await ReadJson(await _factory.CreateClient().SendAsync(
			Form("TodoApi/CreateTodo", new() { ["label"] = "via form", ["priority"] = "High" })));

		Assert.Equal("via form", json.GetProperty("label").GetString());
		Assert.Equal("high", json.GetProperty("priority").GetString());
	}

	[Fact]
	public async Task Form_handlers_report_validation_errors_as_problem_details()
	{
		var response = await _factory.CreateClient().SendAsync(
			Form("TodoApi/CreateTodo", new() { ["label"] = "   ", ["priority"] = "Low" }));

		// RFC 9457 with the ASP.NET `errors` member (HttpValidationProblemDetails).
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		Assert.Equal("A label is required.", json.GetProperty("errors").GetProperty("label")[0].GetString());
	}

	[Fact]
	public async Task Data_annotations_on_parameters_validate_automatically()
	{
		var client = _factory.CreateClient();

		// [EmailAddress] on the C# parameter — no imperative validation in the method.
		var invalid = await client.SendAsync(Form("TodoApi/Subscribe", new() { ["email"] = "not-an-email" }));
		Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
		Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
		var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()).RootElement;
		Assert.True(problem.GetProperty("errors").TryGetProperty("email", out var messages) && messages.GetArrayLength() > 0);

		var valid = await ReadJson(await client.SendAsync(Form("TodoApi/Subscribe", new() { ["email"] = "a@b.com" })));
		Assert.Equal("a@b.com", valid.GetString());
	}

	[Fact]
	public async Task Validate_only_binds_without_running_the_handler()
	{
		var client = _factory.CreateClient();

		var valid = await client.SendAsync(Form("TodoApi/CreateTodo", new() { ["label"] = "x", ["priority"] = "Low" }, validateOnly: true));
		Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);

		var invalid = await client.SendAsync(Form("TodoApi/CreateTodo", new() { ["label"] = "x" }, validateOnly: true));
		Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
		Assert.Contains("priority", await invalid.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Plain_form_posts_without_js_redirect_back()
	{
		var request = Form("TodoApi/CreateTodo", new() { ["label"] = "no js", ["priority"] = "Low" }, enhanced: false);
		request.Headers.Referrer = new Uri("http://localhost/Remote");

		var response = await _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		}).SendAsync(request);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("http://localhost/Remote", response.Headers.Location?.ToString());
	}
}
