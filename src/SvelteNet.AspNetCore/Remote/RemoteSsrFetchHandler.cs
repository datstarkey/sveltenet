namespace SvelteNet.AspNetCore.Remote;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Remote;
using System.Text.Json;

/// <summary>
/// The in-process fetch bridge: resolves /_sveltenet/remote query GETs made inside
/// the SSR engine straight to the [Query] descriptors — no HTTP round-trip. Uses
/// the current request's scope when available so scoped services behave normally.
/// </summary>
internal sealed class RemoteSsrFetchHandler(
	SvelteRemoteRegistry registry,
	IHttpContextAccessor httpContextAccessor,
	IServiceProvider rootProvider) : ISvelteSsrFetchHandler
{
	private const string Prefix = "/_sveltenet/remote/";

	public (int Status, string? Body) Handle(string url, string method)
	{
		if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase)) return (405, null);
		if (!url.StartsWith(Prefix, StringComparison.Ordinal)) return (404, null);

		var rest = url[Prefix.Length..];
		string? argsJson = null;
		var queryIndex = rest.IndexOf('?');
		if (queryIndex >= 0)
		{
			var query = rest[(queryIndex + 1)..];
			rest = rest[..queryIndex];
			foreach (var pair in query.Split('&'))
			{
				if (pair.StartsWith("args=", StringComparison.Ordinal))
					argsJson = Uri.UnescapeDataString(pair["args=".Length..]);
			}
		}

		var parts = rest.Split('/');
		if (parts.Length != 2 ||
		    !registry.TryGet(parts[0], parts[1], out var service, out var descriptor) ||
		    descriptor.Kind != RemoteKind.Query)
			return (404, null);

		var requestServices = httpContextAccessor.HttpContext?.RequestServices;
		using var fallbackScope = requestServices is null ? rootProvider.CreateScope() : null;
		var provider = requestServices ?? fallbackScope!.ServiceProvider;
		var instance = provider.GetRequiredService(service.ServiceType);

		var args = new RemoteArguments
		{
			Json = argsJson is null ? null : JsonDocument.Parse(argsJson).RootElement,
			Validation = SvelteRemoteEndpoints.BuildValidation(provider, service.ServiceType, descriptor)
		};
		var result = descriptor.Invoke(instance, args).AsTask().GetAwaiter().GetResult();

		// Same RFC 9457 shape the HTTP endpoints produce, so the client transport
		// parses SSR-bridged responses identically.
		if (args.Errors is not null)
			return (400, JsonSerializer.Serialize(new
			{
				title = "One or more validation errors occurred.",
				status = 400,
				errors = args.Errors
			}, SvelteJson.Options));
		return result is null ? (204, null) : (200, JsonSerializer.Serialize(result, SvelteJson.Options));
	}
}
