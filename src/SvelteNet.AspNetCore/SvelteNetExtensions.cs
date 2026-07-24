namespace SvelteNet.AspNetCore;

using System.Reflection;
using System.Runtime.CompilerServices;
using Dev;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Remote;
using SvelteNet.Remote;

public static class SvelteNetExtensions
{
	/// <summary>
	/// Registers SvelteNet. [SvelteRemote] services are discovered from the calling
	/// assembly; pass <paramref name="applicationAssemblies"/> when they live elsewhere.
	/// Rendering is client-only unless an SSR renderer is selected on the returned builder.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static SvelteNetBuilder AddSvelteNet(this IServiceCollection services, Action<SvelteOptions>? configure = null, params Assembly[] applicationAssemblies)
	{
		// The calling assembly is the app itself — the default discovery scope.
		// Scoping matters when several SvelteNet apps share a process
		// (WebApplicationFactory test hosts).
		var defaultAssemblies = applicationAssemblies is { Length: > 0 }
			? applicationAssemblies
			: [Assembly.GetCallingAssembly()];
		var env = services.LastOrDefault(d => d.ServiceType == typeof(IWebHostEnvironment))?.ImplementationInstance
				  as IWebHostEnvironment;
		var options = new SvelteOptions { ContentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory() };
		if (env?.IsDevelopment() == true && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
			options.IsDev = true;
		configure?.Invoke(options);
		options.ApplicationAssemblies ??= defaultAssemblies;
		var assemblies = options.ApplicationAssemblies;
		services.AddSingleton(options);
		services.AddHttpContextAccessor();
		services.AddSingleton<SvelteRenderer>();

		// BYOV: DataAnnotations run on remote-function arguments by default; register
		// additional ISvelteRemoteValidator implementations for FluentValidation etc.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ISvelteRemoteValidator, DataAnnotationsRemoteValidator>());

		// [SvelteRemote] services, registered scoped and dispatched by MapSvelteRemote.
		// The source generator's [ModuleInitializer]s have already registered compiled
		// descriptors by the time the app's Program runs — no reflection scan. The scan
		// only runs as a fallback for apps built without SvelteNet.Generators.
		var descriptors = SvelteRemoteDescriptors.All
			.Where(d => assemblies.Contains(d.ServiceType.Assembly))
			.ToList();
		if (options.EnableReflectionFallback)
		{
			var registeredTypes = descriptors.Select(d => d.ServiceType).ToHashSet();
			descriptors.AddRange(TypeDiscovery
				.FindTypes(assemblies, t =>
					t.IsDefined(typeof(SvelteRemoteAttribute), false) && !registeredTypes.Contains(t))
				.Select(SvelteRemoteDescriptors.FromReflection));
		}

		foreach (var descriptor in descriptors) services.AddScoped(descriptor.ServiceType);
		services.AddSingleton(new SvelteRemoteRegistry(descriptors));

		return new SvelteNetBuilder(services);
	}

	/// <summary>
	/// TypeScript generation normally happens at build time (SvelteNet.Build.targets
	/// runs the scaffolder after every dotnet build). Set EnableScaffolding = true to
	/// additionally scaffold at startup, e.g. when the MSBuild targets aren't wired up.
	/// </summary>
	public static IApplicationBuilder UseSvelteNet(this IApplicationBuilder app)
	{
		var options = app.ApplicationServices.GetRequiredService<SvelteOptions>();
		if (options.EnableScaffolding == true) SvelteScaffolder.Run(options);
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
