namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Boots the TodoApp sample for integration tests: dev mode (no Vite build needed)
/// with scaffolding disabled so the sample's source tree is never touched.
/// </summary>
public class TodoAppFactory : WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");
		builder.ConfigureTestServices(services =>
			services.AddSingleton(new SvelteOptions { IsDev = true, EnableScaffolding = false }));
	}
}
