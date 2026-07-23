namespace SvelteNet.AspNetCore;

using Dev;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
}
