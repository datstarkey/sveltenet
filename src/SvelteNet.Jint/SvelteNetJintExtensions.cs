namespace SvelteNet.AspNetCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers the in-process Jint renderer. Jint ships in its own package so that
/// client-only applications, and applications rendering through Node.js or Bun, do not
/// carry a JavaScript engine they never load.
/// </summary>
public static class SvelteNetJintExtensions
{
	/// <summary>
	/// Uses the in-process Jint JavaScript engine for server rendering. No external
	/// runtime is required. Awaited remote queries resolve through the in-process fetch
	/// bridge rather than a loopback HTTP request.
	/// </summary>
	public static SvelteNetBuilder AddJintSSR(this SvelteNetBuilder builder, Action<JintSsrOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new JintSsrOptions();
		configure?.Invoke(options);
		builder.Services.Replace(ServiceDescriptor.Singleton(options));
		builder.AddInProcessSsrFetchBridge();
		return builder.AddCustomRenderer<JintSsrEngine>();
	}
}
