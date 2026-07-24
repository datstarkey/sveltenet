namespace SvelteNet.AspNetCore.Remote;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Remote;

internal static class SvelteRemoteEndpoints
{
	public static async Task<IResult> HandleGet(string service, string method, HttpContext context)
	{
		if (!TryResolve(context, service, method, RemoteKind.Query, out var serviceType, out var descriptor, out var instance))
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

		return await Run(descriptor, instance, new RemoteArguments
		{
			Json = json,
			CancellationToken = context.RequestAborted,
			Validation = BuildValidation(context.RequestServices, serviceType, descriptor)
		});
	}

	public static async Task<IResult> HandlePost(string service, string method, HttpContext context)
	{
		var enhanced = context.Request.Headers.ContainsKey("X-SvelteNet");

		if (context.Request.HasFormContentType)
		{
			if (!TryResolve(context, service, method, RemoteKind.Form, out var serviceType, out var descriptor, out var instance))
				return Results.NotFound();

			// Plain form posts can't set custom headers — require same-origin instead.
			if (!enhanced && !IsSameOrigin(context))
				return Results.Problem(detail: "Cross-origin form post rejected.", statusCode: StatusCodes.Status400BadRequest);

			var form = await context.Request.ReadFormAsync(context.RequestAborted);
			var args = new RemoteArguments
			{
				Form = form.ToDictionary(
						kv => kv.Key,
						kv => (IReadOnlyList<string>)kv.Value.Select(v => v ?? string.Empty).ToArray(),
						StringComparer.Ordinal),
				ValidateOnly = context.Request.Headers.ContainsKey("X-SvelteNet-Validate"),
				CancellationToken = context.RequestAborted,
				Validation = BuildValidation(context.RequestServices, serviceType, descriptor)
			};

			// Without JS the browser posted directly — run the mutation, then send it back.
			if (!enhanced)
			{
				var result = await Run(descriptor, instance, args);
				if (args.Errors is not null) return result;
				return Results.Redirect(LocalReturnUrl(context));
			}

			return await Run(descriptor, instance, args);
		}

		if (!enhanced)
			return Results.Problem(detail: "Missing X-SvelteNet header.", statusCode: StatusCodes.Status400BadRequest);

		if (!TryResolve(context, service, method, RemoteKind.Command, out var commandServiceType, out var commandDescriptor, out var commandInstance))
			return Results.NotFound();

		JsonElement? body = null;
		if (context.Request.ContentLength != 0)
		{
			try
			{
				body = (await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)).RootElement;
			}
			catch (JsonException)
			{
				return Results.Problem(detail: "The JSON request body is malformed.", statusCode: StatusCodes.Status400BadRequest);
			}
		}

		return await Run(commandDescriptor, commandInstance, new RemoteArguments
		{
			Json = body,
			CancellationToken = context.RequestAborted,
			Validation = BuildValidation(context.RequestServices, commandServiceType, commandDescriptor)
		});
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
			ApplyValidationException(args, invalid);
		}

		// RFC 9457 problem details with the standard ASP.NET `errors` member
		// (HttpValidationProblemDetails) — application/problem+json, status 400.
		if (args.Errors is not null)
			return Results.ValidationProblem(args.Errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
		if (args.ValidateOnly)
			return Results.NoContent();

		return result is null ? Results.NoContent() : Results.Json(result, SvelteJson.Options);
	}

	internal static void ApplyValidationException(RemoteArguments args, SvelteValidationException invalid)
	{
		foreach (var (field, messages) in invalid.Errors)
			foreach (var message in messages)
				args.AddError(field.Length == 0 ? "" : TypeGen.StringExtensions.ToCamelCase(field), message);
	}

	private static bool TryResolve(HttpContext context, string service, string method, RemoteKind kind, out Type serviceType, out RemoteMethodDescriptor descriptor, out object instance)
	{
		serviceType = null!;
		descriptor = null!;
		instance = null!;
		var registry = context.RequestServices.GetRequiredService<SvelteRemoteRegistry>();
		if (!registry.TryGet(service, method, out var serviceDescriptor, out descriptor!) || descriptor.Kind != kind)
			return false;
		serviceType = serviceDescriptor.ServiceType;
		instance = context.RequestServices.GetRequiredService(serviceDescriptor.ServiceType);
		return true;
	}

	/// <summary>
	/// Composes the registered ISvelteRemoteValidators into the pipeline dispatchers
	/// await between binding and invocation. Null when none are registered.
	/// </summary>
	internal static Func<RemoteArguments, ValueTask>? BuildValidation(IServiceProvider services, Type serviceType, RemoteMethodDescriptor method)
	{
		var validators = services.GetServices<ISvelteRemoteValidator>().ToArray();
		if (validators.Length == 0) return null;
		return async args =>
		{
			var context = new RemoteValidationContext(serviceType, method, args);
			foreach (var validator in validators) await validator.ValidateAsync(context);
		};
	}

	private static bool IsSameOrigin(HttpContext context)
	{
		var origin = context.Request.Headers.Origin.ToString();
		if (origin.Length > 0) return IsRequestOrigin(context, origin);

		var referer = context.Request.Headers.Referer.ToString();
		return referer.Length > 0 && IsRequestOrigin(context, referer);
	}

	private static bool IsRequestOrigin(HttpContext context, string value) =>
		Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
		string.Equals(uri.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase) &&
		string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase);

	private static string LocalReturnUrl(HttpContext context)
	{
		var referer = context.Request.Headers.Referer.ToString();
		if (!IsRequestOrigin(context, referer) || !Uri.TryCreate(referer, UriKind.Absolute, out var uri))
			return "/";
		return uri.PathAndQuery.Length > 0 ? uri.PathAndQuery : "/";
	}
}
