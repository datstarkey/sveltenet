namespace SvelteNet.AspNetCore.Remote;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Remote;

/// <summary>
/// The in-process fetch bridge: resolves /_sveltenet/remote query GETs made inside
/// the SSR engine straight to the [Query] descriptors — no HTTP round-trip. Uses
/// the current request's scope when available so scoped services behave normally.
/// </summary>
/// <remarks>
/// Because this path skips routing, it also skips endpoint filters — so it enforces the
/// descriptor's own authorization metadata itself. That metadata is the same data
/// <see cref="SvelteNetExtensions.MapSvelteRemote"/> projects onto the HTTP endpoints,
/// which is what keeps SSR and HTTP from disagreeing about who may call a query.
/// </remarks>
internal sealed class RemoteSsrFetchHandler(
	SvelteRemoteRegistry registry,
	IHttpContextAccessor httpContextAccessor,
	IServiceProvider rootProvider) : ISvelteSsrFetchHandler
{
	private const string Prefix = "/_sveltenet/remote/";

	public async ValueTask<SsrFetchResponse> HandleAsync(string url, string method, CancellationToken cancellationToken = default)
	{
		if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase)) return SsrFetchResponse.MethodNotAllowed;
		if (!url.StartsWith(Prefix, StringComparison.Ordinal)) return SsrFetchResponse.NotFound;

		var rest = url[Prefix.Length..];
		string? argsJson = null;
		var queryIndex = rest.IndexOf('?');
		if (queryIndex >= 0)
		{
			var parsed = QueryHelpers.ParseQuery(rest[(queryIndex + 1)..]);
			rest = rest[..queryIndex];
			if (parsed.TryGetValue("args", out var values) && values.Count > 0) argsJson = values[0];
		}

		var parts = rest.Split('/');
		if (parts.Length != 2 ||
			!registry.TryGet(parts[0], parts[1], out var service, out var descriptor) ||
			descriptor.Kind != RemoteKind.Query)
			return SsrFetchResponse.NotFound;

		var httpContext = httpContextAccessor.HttpContext;
		var requestServices = httpContext?.RequestServices;
		using var fallbackScope = requestServices is null ? rootProvider.CreateScope() : null;
		var provider = requestServices ?? fallbackScope!.ServiceProvider;

		// Same policy the HTTP endpoint would apply. Without this, a query protected by
		// [Authorize] would still run — and leak into the server-rendered HTML — for an
		// anonymous visitor.
		var authorization = await RemoteAuthorizationPolicy
			.AuthorizeAsync(provider, httpContext, service, descriptor)
			.ConfigureAwait(false);
		if (authorization is not null) return authorization.Value;

		JsonElement? args = null;
		JsonDocument? argsDocument = null;
		try
		{
			if (argsJson is not null)
			{
				argsDocument = JsonDocument.Parse(argsJson);
				args = argsDocument.RootElement;
			}
		}
		catch (JsonException)
		{
			argsDocument?.Dispose();
			return new SsrFetchResponse(400, JsonSerializer.Serialize(new
			{
				title = "Malformed args.",
				status = 400
			}, SvelteJson.Options));
		}

		using (argsDocument)
		{
			var instance = provider.GetRequiredService(service.ServiceType);
			var arguments = new RemoteArguments
			{
				Json = args,
				Validation = SvelteRemoteEndpoints.BuildValidation(provider, service.ServiceType, descriptor),
				CancellationToken = cancellationToken
			};

			object? result = null;
			try
			{
				result = await descriptor.Invoke(instance, arguments).ConfigureAwait(false);
			}
			catch (SvelteValidationException invalid)
			{
				SvelteRemoteEndpoints.ApplyValidationException(arguments, invalid);
			}

			// Same RFC 9457 shape the HTTP endpoints produce, so the client transport
			// parses SSR-bridged responses identically.
			if (arguments.Errors is not null)
				return new SsrFetchResponse(400, JsonSerializer.Serialize(new
				{
					title = "One or more validation errors occurred.",
					status = 400,
					errors = arguments.Errors
				}, SvelteJson.Options));

			return result is null
				? SsrFetchResponse.NoContent
				: SsrFetchResponse.Json(JsonSerializer.Serialize(result, SvelteJson.Options));
		}
	}
}
