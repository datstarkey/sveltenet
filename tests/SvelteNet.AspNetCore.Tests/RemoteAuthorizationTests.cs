namespace SvelteNet.AspNetCore.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// End-to-end authorization for remote functions, against the RemoteFunctions sample's
/// AdminApi ([Authorize(Policy = "admin")] on the class, [AllowAnonymous] on one method).
///
/// The equivalent policy on the SSR bridge — which never passes through routing — is
/// covered by <see cref="RemoteSsrFetchHandlerTests"/>. Both read the same descriptor
/// metadata, which is the point: the transport cannot change who is allowed in.
/// </summary>
public class RemoteAuthorizationTests : IClassFixture<RemoteFunctionsFactory>
{
	private readonly RemoteFunctionsFactory _factory;

	public RemoteAuthorizationTests(RemoteFunctionsFactory factory)
	{
		_factory = factory;
	}

	private HttpClient CreateClient() =>
		_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

	private static async Task<HttpClient> SignedIn(HttpClient client, string role)
	{
		var response = await client.PostAsync($"/demo-login?role={role}", content: null);
		response.EnsureSuccessStatusCode();
		return client;
	}

	[Fact]
	public async Task Anonymous_callers_cannot_reach_a_protected_query()
	{
		var response = await CreateClient().GetAsync("/_sveltenet/remote/AdminApi/GetAuditLog");

		Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.DoesNotContain("deleted todo", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Anonymous_callers_cannot_reach_a_protected_command()
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "/_sveltenet/remote/AdminApi/PurgeAuditLog")
		{
			Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
			Headers = { { "X-SvelteNet", "true" } }
		};

		var response = await CreateClient().SendAsync(request);

		Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
	}

	/// <summary>[AllowAnonymous] on a method opts it out of the service-level policy.</summary>
	[Fact]
	public async Task Allow_anonymous_methods_stay_public()
	{
		var response = await CreateClient().GetAsync("/_sveltenet/remote/AdminApi/GetServiceStatus");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("ok", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Users_satisfying_the_policy_are_allowed_through()
	{
		var client = await SignedIn(CreateClient(), "admin");

		var response = await client.GetAsync("/_sveltenet/remote/AdminApi/GetAuditLog");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("deleted todo", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Authenticated_users_missing_the_policy_are_forbidden()
	{
		var client = await SignedIn(CreateClient(), "viewer");

		var response = await client.GetAsync("/_sveltenet/remote/AdminApi/GetAuditLog");

		Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.DoesNotContain("deleted todo", await response.Content.ReadAsStringAsync());
	}

	/// <summary>Services without [Authorize] must not become protected by accident.</summary>
	[Fact]
	public async Task Unprotected_services_are_unaffected()
	{
		var response = await CreateClient().GetAsync("/_sveltenet/remote/TodoApi/GetTodos");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
