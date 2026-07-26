namespace SvelteNet.AspNetCore;

using System.Reflection;
using System.Runtime.CompilerServices;
using Dev;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remote;
using SvelteNet.Remote;

public static class SvelteNetExtensions
{
	/// <summary>
	/// Registers SvelteNet. [SvelteRemote] services are discovered from the calling
	/// assembly; pass <paramref name="applicationAssemblies"/> when they live elsewhere —
	/// including whenever this call is wrapped in your own extension method, since the
	/// caller is then that method's assembly rather than the application's.
	/// Rendering is client-only unless an SSR renderer is selected on the returned builder.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static SvelteNetBuilder AddSvelteNet(this IServiceCollection services, Action<SvelteOptions>? configure = null, params Assembly[] applicationAssemblies)
	{
		// The calling assembly is normally the app itself, and scoping matters when
		// several SvelteNet apps share a process (WebApplicationFactory test hosts).
		// It is a guess, though: wrapping this call in an extension method moves it to
		// the wrapper's assembly. ApplicationAssemblies, the explicit parameter, and the
		// entry assembly are all preferred over it.
		var defaultAssemblies = applicationAssemblies is { Length: > 0 }
			? applicationAssemblies
			: [Assembly.GetCallingAssembly()];
		var env = ResolveEnvironment(services);
		var options = new SvelteOptions { ContentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory() };
		if (env?.IsDevelopment() == true) options.IsDev = true;
		configure?.Invoke(options);
		options.ApplicationAssemblies ??= defaultAssemblies;
		var assemblies = options.ApplicationAssemblies;

		if (assemblies.Count == 0)
			throw new InvalidOperationException(
				"SvelteNet has no assemblies to discover types in. Pass them to AddSvelteNet, or set " +
				"SvelteOptions.ApplicationAssemblies.");
		services.AddSingleton(options);
		services.AddHttpContextAccessor();
		services.AddSingleton<SvelteRenderer>();

		// Razor Pages validates antiforgery on POST by default, but only reads the token
		// from a form field unless a header name is configured — and it configures none.
		// enhance() sends the header so users don't have to hand-add a hidden input to
		// every form. An explicit app setting is left alone.
		services.AddAntiforgery();
		services.Configure<AntiforgeryOptions>(antiforgery =>
			antiforgery.HeaderName ??= "RequestVerificationToken");

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
	/// The hosting environment, which is not resolvable yet — the provider does not exist
	/// during registration. WebApplicationBuilder registers it as an instance, so it can be
	/// read off the collection; anything else falls back to the standard environment
	/// variables rather than guessing.
	/// </summary>
	private static IWebHostEnvironment? ResolveEnvironment(IServiceCollection services)
	{
		var registered = services.LastOrDefault(d => d.ServiceType == typeof(IWebHostEnvironment))?.ImplementationInstance
			as IWebHostEnvironment;
		if (registered is not null) return registered;

		var name = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
			?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
		return name is null ? null : new ResolvedEnvironment(name);
	}

	private sealed class ResolvedEnvironment(string environmentName) : IWebHostEnvironment
	{
		public string EnvironmentName { get; set; } = environmentName;
		public string ApplicationName { get; set; } = string.Empty;
		public string WebRootPath { get; set; } = string.Empty;
		public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
		public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}

	/// <summary>
	/// TypeScript generation normally happens at build time (SvelteNet.Build.targets
	/// runs the scaffolder after every dotnet build). Set EnableScaffolding = true to
	/// additionally scaffold at startup, e.g. when the MSBuild targets aren't wired up.
	/// </summary>
	public static IApplicationBuilder UseSvelteNet(this IApplicationBuilder app)
	{
		var options = app.ApplicationServices.GetRequiredService<SvelteOptions>();
		if (options.EnableScaffolding != true) return app;

		// Scaffolding writes into the content root. That is a development activity; doing
		// it at boot in Production would mean an app rewriting its own source tree on a
		// read-only or shared deployment.
		var environment = app.ApplicationServices.GetService<IWebHostEnvironment>();
		if (environment is not null && !environment.IsDevelopment())
		{
			app.ApplicationServices.GetService<ILoggerFactory>()?
				.CreateLogger("SvelteNet")
				.LogWarning(
					"EnableScaffolding is on but the environment is {Environment}. Startup scaffolding only runs in " +
					"Development; types are normally generated at build time by SvelteNet.Build.targets.",
					environment.EnvironmentName);
			return app;
		}

		SvelteScaffolder.Run(options);
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
	///
	/// One endpoint is mapped per remote method rather than a single catch-all route, so
	/// each method's own [Authorize]/[AllowAnonymous] becomes real endpoint metadata and
	/// the standard authorization middleware enforces it. The SSR bridge evaluates the
	/// same descriptor metadata, so both transports agree.
	/// </summary>
	public static IEndpointConventionBuilder MapSvelteRemote(this IEndpointRouteBuilder endpoints, string basePath = "/_sveltenet/remote")
	{
		var registry = endpoints.ServiceProvider.GetRequiredService<SvelteRemoteRegistry>();
		var group = endpoints.MapGroup(basePath.TrimEnd('/'));

		foreach (var service in registry.Services)
		{
			foreach (var method in service.Methods)
			{
				var route = $"/{service.Name}/{method.Name}";
				var metadata = RemoteAuthorizationPolicy.ToMetadata(service, method).ToArray();
				var serviceName = service.Name;
				var methodName = method.Name;

				// Typed as Delegate on purpose. A lambda taking only HttpContext also
				// converts to RequestDelegate, and that overload wins — which would run the
				// handler and silently throw away the IResult, answering 200 with no body.
				Delegate handler = method.Kind == RemoteKind.Query
					? (HttpContext context) => SvelteRemoteEndpoints.HandleGet(serviceName, methodName, context)
					: (HttpContext context) => SvelteRemoteEndpoints.HandlePost(serviceName, methodName, context);

				var endpoint = method.Kind == RemoteKind.Query
					? group.MapGet(route, handler)
					: group.MapPost(route, handler);

				endpoint.WithName($"SvelteNet.Remote.{serviceName}.{methodName}");
				if (metadata.Length > 0) endpoint.WithMetadata(metadata);
			}
		}

		return group;
	}
}
