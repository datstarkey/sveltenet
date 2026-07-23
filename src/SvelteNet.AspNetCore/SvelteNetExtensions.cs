namespace SvelteNet.AspNetCore;

using Dev;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Remote;
using SvelteNet.Remote;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class SvelteNetExtensions
{
	/// <summary>
	/// Registers SvelteNet. [SvelteRemote] services are discovered from the calling
	/// assembly; pass <paramref name="applicationAssemblies"/> when they live elsewhere.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static IServiceCollection AddSvelteNet(this IServiceCollection services, Action<SvelteOptions>? configure = null, params Assembly[] applicationAssemblies)
	{
		// The calling assembly is the app itself — the default discovery scope.
		// Scoping matters when several SvelteNet apps share a process
		// (WebApplicationFactory test hosts).
		var assemblies = applicationAssemblies is { Length: > 0 }
			? applicationAssemblies
			: [Assembly.GetCallingAssembly()];

		services.AddSingleton(sp =>
		{
			var env = sp.GetRequiredService<IWebHostEnvironment>();
			var options = new SvelteOptions { ContentRoot = env.ContentRootPath };

			// Dev mode = load from the Vite dev server. Containers get production behavior
			// even under the Development environment (no dev server available inside).
			if (env.IsDevelopment() && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
				options.IsDev = true;

			configure?.Invoke(options);
			options.ApplicationAssemblies ??= assemblies;
			return options;
		});
		services.AddHttpContextAccessor();
		services.AddSingleton<ISvelteSsrFetchHandler, RemoteSsrFetchHandler>();
		services.AddSingleton<ISvelteSsrEngine, JintSsrEngine>();
		services.AddSingleton<SvelteRenderer>();

		// [SvelteRemote] services, registered scoped and dispatched by MapSvelteRemote.
		// The source generator's [ModuleInitializer]s have already registered compiled
		// descriptors by the time the app's Program runs — no reflection scan. The scan
		// only runs as a fallback for apps built without SvelteNet.Generators.
		var descriptors = SvelteRemoteDescriptors.All
			.Where(d => assemblies.Contains(d.ServiceType.Assembly))
			.ToList();
		if (descriptors.Count == 0)
			descriptors = TypeDiscovery
				.FindTypes(assemblies, t => t.IsDefined(typeof(SvelteRemoteAttribute), false))
				.Select(SvelteRemoteDescriptors.For)
				.ToList();

		foreach (var descriptor in descriptors) services.AddScoped(descriptor.ServiceType);
		services.AddSingleton(new SvelteRemoteRegistry(descriptors));

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
