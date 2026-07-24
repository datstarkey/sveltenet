namespace SvelteNet.AspNetCore.Tests;

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

	[Fact]
	public void Throwing_validation_uses_the_same_problem_shape_as_http_dispatch()
	{
		var method = new RemoteMethodDescriptor(
			"Run",
			RemoteKind.Query,
			static (service, _) => ValueTask.FromResult<object?>(((ValidationQuery)service).Run()),
			typeof(string),
			[]);
		var registry = new SvelteRemoteRegistry(
		[
			new RemoteServiceDescriptor("ValidationQuery", typeof(ValidationQuery), [method], IsGenerated: true)
		]);
		var services = new ServiceCollection()
			.AddScoped<ValidationQuery>()
			.BuildServiceProvider();
		using var scope = services.CreateScope();
		var accessor = new HttpContextAccessor
		{
			HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider }
		};
		var handler = new RemoteSsrFetchHandler(registry, accessor, services);

		var (status, body) = handler.Handle("/_sveltenet/remote/ValidationQuery/Run", "GET");

		Assert.Equal(400, status);
		var problem = JsonDocument.Parse(body!).RootElement;
		Assert.Equal("Value is invalid.", problem.GetProperty("errors").GetProperty("value")[0].GetString());
	}
}
