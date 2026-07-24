namespace SvelteNet.AspNetCore.Remote;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Remote;
using System.Text.Json;

internal static class SvelteRemoteEndpoints
{
	public static async Task<IResult> HandleGet(string service, string method, HttpContext context)
	{
		if (!TryResolve(context, service, method, RemoteKind.Query, out var descriptor, out var instance))
			return Results.NotFound();

		JsonElement? json = null;
		if (context.Request.Query.TryGetValue("args", out var raw) && raw.Count > 0)
		{
			try
			{
				json = JsonDocument.Parse(raw[0]!).RootElement;
			}
			catch (JsonException)
			{
				return Results.Problem(detail: "Malformed args.", statusCode: StatusCodes.Status400BadRequest);
			}
		}

		return await Run(descriptor, instance, new RemoteArguments { Json = json, CancellationToken = context.RequestAborted });
	}

	public static async Task<IResult> HandlePost(string service, string method, HttpContext context)
	{
		var enhanced = context.Request.Headers.ContainsKey("X-SvelteNet");

		if (context.Request.HasFormContentType)
		{
			if (!TryResolve(context, service, method, RemoteKind.Form, out var descriptor, out var instance))
				return Results.NotFound();

			// Plain form posts can't set custom headers — require same-origin instead.
			if (!enhanced && !IsSameOrigin(context))
				return Results.Problem(detail: "Cross-origin form post rejected.", statusCode: StatusCodes.Status400BadRequest);

			var form = await context.Request.ReadFormAsync(context.RequestAborted);
			var args = new RemoteArguments
			{
				Form = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
				ValidateOnly = context.Request.Headers.ContainsKey("X-SvelteNet-Validate"),
				CancellationToken = context.RequestAborted
			};

			// Without JS the browser posted directly — run the mutation, then send it back.
			if (!enhanced)
			{
				await Run(descriptor, instance, args);
				return Results.Redirect(context.Request.Headers.Referer.ToString() is { Length: > 0 } referer ? referer : "/");
			}

			return await Run(descriptor, instance, args);
		}

		if (!enhanced)
			return Results.Problem(detail: "Missing X-SvelteNet header.", statusCode: StatusCodes.Status400BadRequest);

		if (!TryResolve(context, service, method, RemoteKind.Command, out var commandDescriptor, out var commandInstance))
			return Results.NotFound();

		JsonElement? body = null;
		if (context.Request.ContentLength is > 0)
			body = (await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)).RootElement;

		return await Run(commandDescriptor, commandInstance, new RemoteArguments { Json = body, CancellationToken = context.RequestAborted });
	}

	private static async Task<IResult> Run(RemoteMethodDescriptor descriptor, object instance, RemoteArguments args)
	{
		object? result = null;
		try
		{
			result = await descriptor.Invoke(instance, args);
		}
		catch (SvelteValidationException invalid)
		{
			foreach (var (field, messages) in invalid.Errors)
			foreach (var message in messages)
				args.AddError(field.Length == 0 ? "" : TypeGen.StringExtensions.ToCamelCase(field), message);
		}

		// RFC 9457 problem details with the standard ASP.NET `errors` member
		// (HttpValidationProblemDetails) — application/problem+json, status 400.
		if (args.Errors is not null)
			return Results.ValidationProblem(args.Errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
		if (args.ValidateOnly)
			return Results.NoContent();

		return result is null ? Results.NoContent() : Results.Json(result, SvelteJson.Options);
	}

	private static bool TryResolve(HttpContext context, string service, string method, RemoteKind kind, out RemoteMethodDescriptor descriptor, out object instance)
	{
		descriptor = null!;
		instance = null!;
		var registry = context.RequestServices.GetRequiredService<SvelteRemoteRegistry>();
		if (!registry.TryGet(service, method, out var serviceDescriptor, out descriptor!) || descriptor.Kind != kind)
			return false;
		instance = context.RequestServices.GetRequiredService(serviceDescriptor.ServiceType);
		return true;
	}

	private static bool IsSameOrigin(HttpContext context)
	{
		var origin = context.Request.Headers.Origin.ToString();
		if (origin.Length == 0) return true;
		return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
		       string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase);
	}
}
