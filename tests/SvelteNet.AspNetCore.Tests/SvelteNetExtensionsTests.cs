namespace SvelteNet.AspNetCore.Tests;

using Microsoft.Extensions.DependencyInjection;
using SvelteNet.AspNetCore.Remote;
using SvelteNet.AspNetCore.Tests.Fixtures;

public class SvelteNetExtensionsTests
{
	private sealed class CustomEngine : ISvelteSsrEngine
	{
		public SsrResult Render(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default) => new();
	}

	[Fact]
	public void AddSvelteNet_is_client_only_by_default()
	{
		var services = new ServiceCollection();

		services.AddSvelteNet();

		Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISvelteSsrEngine));
		Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISvelteSsrFetchHandler));
		using var provider = services.BuildServiceProvider();
		Assert.NotNull(provider.GetRequiredService<SvelteRenderer>());
	}

	[Fact]
	public void User_registered_ssr_engine_is_preserved()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ISvelteSsrEngine, CustomEngine>();

		services.AddSvelteNet();

		using var provider = services.BuildServiceProvider();
		Assert.IsType<CustomEngine>(provider.GetRequiredService<ISvelteSsrEngine>());
	}

	[Theory]
	[InlineData("jint", typeof(JintSsrEngine))]
	[InlineData("node", typeof(NodeSsrEngine))]
	[InlineData("bun", typeof(BunJsSsrEngine))]
	public void Ssr_backend_is_selected_explicitly(string backend, Type expectedType)
	{
		var services = new ServiceCollection();
		var builder = services.AddSvelteNet();

		switch (backend)
		{
			case "jint":
				builder.AddJintSSR();
				break;
			case "node":
				builder.AddNodeSSR();
				break;
			case "bun":
				builder.AddBunJsSSR();
				break;
		}

		var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISvelteSsrEngine));
		Assert.Equal(expectedType, registration.ImplementationType);
	}

	[Fact]
	public void Backend_options_are_owned_by_the_backend_registration()
	{
		var services = new ServiceCollection();

		services.AddSvelteNet().AddNodeSSR(options =>
		{
			options.ExecutablePath = "/opt/node";
			options.Timeout = TimeSpan.FromSeconds(4);
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<NodeSsrOptions>();
		Assert.Equal("/opt/node", options.ExecutablePath);
		Assert.Equal(TimeSpan.FromSeconds(4), options.Timeout);
	}

	[Fact]
	public void Custom_renderer_can_be_registered_by_type()
	{
		var services = new ServiceCollection();

		services.AddSvelteNet().AddCustomRenderer<CustomEngine>();

		var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISvelteSsrEngine));
		Assert.Equal(typeof(CustomEngine), registration.ImplementationType);
	}

	[Fact]
	public void Application_assemblies_configured_in_options_scope_remote_registration()
	{
		var services = new ServiceCollection();

		services.AddSvelteNet(options => options.ApplicationAssemblies = [typeof(WidgetApi).Assembly]);

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<SvelteRemoteRegistry>();
		Assert.Contains(registry.Services, descriptor => descriptor.ServiceType == typeof(WidgetApi));
	}
}
