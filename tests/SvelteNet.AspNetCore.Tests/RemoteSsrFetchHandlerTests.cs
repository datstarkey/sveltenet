namespace SvelteNet.AspNetCore.Tests;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.AspNetCore.Remote;
using SvelteNet.Remote;

public class RemoteSsrFetchHandlerTests
{
	private sealed class ValidationQuery
	{
		public string Run() => throw new SvelteValidationException("value", "Value is invalid.");
	}

	private sealed class SecretQuery
	{
		public string Run() => "top secret";
	}

	private static RemoteMethodDescriptor Method<TService>(
		Func<TService, string> run,
		RemoteAuthorization[]? authorization = null,
		bool allowAnonymous = false) =>
		new(
			"Run",
			RemoteKind.Query,
			(service, _) => ValueTask.FromResult<object?>(run((TService)service)),
			typeof(string),
			[],
			TypeScriptReturnType: null,
			Authorization: authorization,
			AllowAnonymous: allowAnonymous);

	private static (RemoteSsrFetchHandler Handler, ServiceProvider Services) Build(
		RemoteServiceDescriptor descriptor,
		Action<IServiceCollection> configure,
		ClaimsPrincipal? user = null)
	{
		var registry = new SvelteRemoteRegistry([descriptor]);
		var collection = new ServiceCollection();
		configure(collection);
		var services = collection.BuildServiceProvider();
		var scope = services.CreateScope();
		var accessor = new HttpContextAccessor
		{
			HttpContext = new DefaultHttpContext
			{
				RequestServices = scope.ServiceProvider,
				User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
			}
		};
		return (new RemoteSsrFetchHandler(registry, accessor, services), services);
	}

	[Fact]
	public async Task Throwing_validation_uses_the_same_problem_shape_as_http_dispatch()
	{
		var descriptor = new RemoteServiceDescriptor(
			"ValidationQuery",
			typeof(ValidationQuery),
			[Method<ValidationQuery>(s => s.Run())],
			IsGenerated: true);
		var (handler, services) = Build(descriptor, s => s.AddScoped<ValidationQuery>());
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/ValidationQuery/Run", "GET");

		Assert.Equal(400, response.Status);
		var problem = JsonDocument.Parse(response.Body!).RootElement;
		Assert.Equal("Value is invalid.", problem.GetProperty("errors").GetProperty("value")[0].GetString());
	}

	/// <summary>
	/// The SSR bridge dispatches descriptors directly and never passes through routing, so
	/// it has to apply the descriptor's own [Authorize] itself. Without this an anonymous
	/// page view would run a protected query and bake the result into the HTML.
	/// </summary>
	[Fact]
	public async Task Protected_queries_are_refused_for_anonymous_users()
	{
		var descriptor = new RemoteServiceDescriptor(
			"SecretQuery",
			typeof(SecretQuery),
			[Method<SecretQuery>(s => s.Run())],
			IsGenerated: true,
			Authorization: [new RemoteAuthorization()]);
		var (handler, services) = Build(descriptor, s =>
		{
			s.AddScoped<SecretQuery>();
			s.AddAuthorization();
			s.AddLogging();
		});
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/SecretQuery/Run", "GET");

		Assert.Equal(401, response.Status);
		Assert.DoesNotContain("top secret", response.Body ?? string.Empty);
	}

	[Fact]
	public async Task Protected_queries_run_for_authenticated_users()
	{
		var descriptor = new RemoteServiceDescriptor(
			"SecretQuery",
			typeof(SecretQuery),
			[Method<SecretQuery>(s => s.Run())],
			IsGenerated: true,
			Authorization: [new RemoteAuthorization()]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "jake")], "test"));
		var (handler, services) = Build(
			descriptor,
			s =>
			{
				s.AddScoped<SecretQuery>();
				s.AddAuthorization();
				s.AddLogging();
			},
			user);
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/SecretQuery/Run", "GET");

		Assert.Equal(200, response.Status);
		Assert.Contains("top secret", response.Body!);
	}

	[Fact]
	public async Task Authenticated_but_unauthorized_users_are_forbidden()
	{
		var descriptor = new RemoteServiceDescriptor(
			"SecretQuery",
			typeof(SecretQuery),
			[Method<SecretQuery>(s => s.Run())],
			IsGenerated: true,
			Authorization: [new RemoteAuthorization(Policy: "admin")]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "jake")], "test"));
		var (handler, services) = Build(
			descriptor,
			s =>
			{
				s.AddScoped<SecretQuery>();
				s.AddAuthorization(options =>
					options.AddPolicy("admin", policy => policy.RequireClaim(ClaimTypes.Role, "admin")));
				s.AddLogging();
			},
			user);
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/SecretQuery/Run", "GET");

		Assert.Equal(403, response.Status);
		Assert.DoesNotContain("top secret", response.Body ?? string.Empty);
	}

	[Fact]
	public async Task Method_level_allow_anonymous_opts_out_of_the_service_policy()
	{
		var descriptor = new RemoteServiceDescriptor(
			"SecretQuery",
			typeof(SecretQuery),
			[Method<SecretQuery>(s => s.Run(), allowAnonymous: true)],
			IsGenerated: true,
			Authorization: [new RemoteAuthorization()]);
		var (handler, services) = Build(descriptor, s =>
		{
			s.AddScoped<SecretQuery>();
			s.AddAuthorization();
			s.AddLogging();
		});
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/SecretQuery/Run", "GET");

		Assert.Equal(200, response.Status);
	}

	[Fact]
	public async Task Malformed_args_produce_a_problem_response_rather_than_throwing()
	{
		var descriptor = new RemoteServiceDescriptor(
			"SecretQuery",
			typeof(SecretQuery),
			[Method<SecretQuery>(s => s.Run())],
			IsGenerated: true);
		var (handler, services) = Build(descriptor, s => s.AddScoped<SecretQuery>());
		using var _ = services;

		var response = await handler.HandleAsync("/_sveltenet/remote/SecretQuery/Run?args=%7Bnot-json", "GET");

		Assert.Equal(400, response.Status);
	}
}
