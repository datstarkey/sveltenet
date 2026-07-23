namespace SvelteNet.AspNetCore;

using Dev;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Remote;

public static class SvelteNetExtensions
{
	public static IServiceCollection AddSvelteNet(this IServiceCollection services, Action<SvelteOptions>? configure = null)
	{
		services.AddSingleton(sp =>
		{
			var env = sp.GetRequiredService<IWebHostEnvironment>();
			var options = new SvelteOptions { ContentRoot = env.ContentRootPath };

			// Dev mode = load from the Vite dev server. Containers get production behavior
			// even under the Development environment (no dev server available inside).
			if (env.IsDevelopment() && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
				options.IsDev = true;

			configure?.Invoke(options);
			return options;
		});
		services.AddSingleton<ISvelteSsrEngine, JintSsrEngine>();
		services.AddSingleton<SvelteRenderer>();

		// [SvelteRemote] services: registered scoped, dispatched by MapSvelteRemote.
		var remoteServices = TypeDiscovery.FindTypes(t => t.IsDefined(typeof(SvelteRemoteAttribute), false));
		foreach (var service in remoteServices) services.AddScoped(service);
		services.AddSingleton(new SvelteRemoteRegistry(remoteServices));

		return services;
	}

	/// <summary>
	/// In dev mode, generates TypeScript types for all [SvelteProp] models and scaffolds
	/// missing Svelte components, runtime helpers, and Vite config. No-op in production.
	/// </summary>
	public static IApplicationBuilder UseSvelteNet(this IApplicationBuilder app)
	{
		var options = app.ApplicationServices.GetRequiredService<SvelteOptions>();
		if (options.EnableScaffolding ?? options.IsDev) SvelteScaffolder.Run(options);
		return app;
	}

	/// <summary>
	/// Dev-mode scaffolding plus the remote-function endpoints. To customize the
	/// endpoints (e.g. RequireAuthorization), call MapSvelteRemote directly instead.
	/// </summary>
	public static WebApplication UseSvelteNet(this WebApplication app)
	{
		((IApplicationBuilder)app).UseSvelteNet();
		app.MapSvelteRemote();
		return app;
	}

	/// <summary>
	/// Maps the remote-function endpoints under {basePath}/{Service}/{Method}:
	/// [Query] over GET (?args= JSON), [Command] over POST JSON (X-SvelteNet header
	/// required — CSRF defense, custom headers need a CORS preflight), and [Form]
	/// over form POSTs (same-origin checked; without JS the browser is redirected back).
	/// </summary>
	public static IEndpointConventionBuilder MapSvelteRemote(this IEndpointRouteBuilder endpoints, string basePath = "/_sveltenet/remote")
	{
		var group = endpoints.MapGroup(basePath.TrimEnd('/'));
		group.MapGet("/{service}/{method}", (string service, string method, HttpContext context) =>
			SvelteRemoteEndpoints.HandleGet(service, method, context));
		group.MapPost("/{service}/{method}", (string service, string method, HttpContext context) =>
			SvelteRemoteEndpoints.HandlePost(service, method, context));
		return group;
	}
}
