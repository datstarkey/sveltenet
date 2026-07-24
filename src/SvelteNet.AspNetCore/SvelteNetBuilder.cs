namespace SvelteNet.AspNetCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Remote;

/// <summary>
/// Configures optional SvelteNet features after the core client-side services have
/// been registered.
/// </summary>
public sealed class SvelteNetBuilder
{
	internal SvelteNetBuilder(IServiceCollection services)
	{
		Services = services;
	}

	/// <summary>The application service collection.</summary>
	public IServiceCollection Services { get; }

	/// <summary>Uses the in-process Jint JavaScript engine for server rendering.</summary>
	public SvelteNetBuilder AddJintSSR(Action<JintSsrOptions>? configure = null)
	{
		var options = new JintSsrOptions();
		configure?.Invoke(options);
		Services.Replace(ServiceDescriptor.Singleton(options));
		Services.TryAddSingleton<ISvelteSsrFetchHandler, RemoteSsrFetchHandler>();
		return ReplaceSsrEngine<JintSsrEngine>();
	}

	/// <summary>
	/// Uses Node.js for server rendering. The <c>node</c> CLI must be installed and
	/// available on PATH.
	/// </summary>
	public SvelteNetBuilder AddNodeSSR(Action<NodeSsrOptions>? configure = null)
	{
		var options = new NodeSsrOptions();
		configure?.Invoke(options);
		Services.Replace(ServiceDescriptor.Singleton(options));
		return ReplaceSsrEngine<NodeSsrEngine>();
	}

	/// <summary>
	/// Uses Bun for server rendering. The <c>bun</c> CLI must be installed and
	/// available on PATH.
	/// </summary>
	public SvelteNetBuilder AddBunJsSSR(Action<BunJsSsrOptions>? configure = null)
	{
		var options = new BunJsSsrOptions();
		configure?.Invoke(options);
		Services.Replace(ServiceDescriptor.Singleton(options));
		return ReplaceSsrEngine<BunJsSsrEngine>();
	}

	/// <summary>Uses a custom singleton SSR engine resolved from dependency injection.</summary>
	public SvelteNetBuilder AddCustomRenderer<TEngine>()
		where TEngine : class, ISvelteSsrEngine
	{
		return ReplaceSsrEngine<TEngine>();
	}

	/// <summary>Uses an existing custom SSR engine instance.</summary>
	public SvelteNetBuilder AddCustomRenderer(ISvelteSsrEngine renderer)
	{
		ArgumentNullException.ThrowIfNull(renderer);
		Services.Replace(ServiceDescriptor.Singleton(renderer));
		return this;
	}

	/// <summary>Uses a DI factory to create a custom singleton SSR engine.</summary>
	public SvelteNetBuilder AddCustomRenderer(Func<IServiceProvider, ISvelteSsrEngine> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		Services.Replace(ServiceDescriptor.Singleton(factory));
		return this;
	}

	private SvelteNetBuilder ReplaceSsrEngine<TEngine>()
		where TEngine : class, ISvelteSsrEngine
	{
		Services.Replace(ServiceDescriptor.Singleton<ISvelteSsrEngine, TEngine>());
		return this;
	}
}
