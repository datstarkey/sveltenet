namespace SvelteNet.AspNetCore.Remote;

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.Remote;

/// <summary>
/// Turns descriptor authorization metadata into an actual decision.
///
/// The HTTP endpoints get this for free — <see cref="ToMetadata"/> projects the
/// descriptor's requirements onto the endpoint so ASP.NET's own authorization middleware
/// runs. The Jint SSR bridge dispatches descriptors directly and never touches routing,
/// so it calls <see cref="AuthorizeAsync"/> to apply the identical policy. Both paths read
/// the same metadata, which is what keeps "protected over HTTP" and "protected during SSR"
/// from drifting apart.
/// </summary>
internal static class RemoteAuthorizationPolicy
{
	/// <summary>
	/// Endpoint metadata equivalent to the descriptor's [Authorize]/[AllowAnonymous].
	/// A method's own requirements are additive to the service's, matching how MVC and
	/// minimal APIs combine class- and method-level attributes.
	/// </summary>
	public static IEnumerable<object> ToMetadata(RemoteServiceDescriptor service, RemoteMethodDescriptor method)
	{
		foreach (var authorization in Requirements(service, method))
			yield return new AuthorizeAttribute
			{
				Policy = authorization.Policy,
				Roles = authorization.Roles,
				AuthenticationSchemes = authorization.AuthenticationSchemes
			};

		if (method.AllowAnonymous || service.AllowAnonymous) yield return new AllowAnonymousAttribute();
	}

	/// <summary>
	/// Evaluates the descriptor's requirements against the current user.
	/// Returns null when the call is allowed, or the response the caller should return.
	/// </summary>
	public static async ValueTask<SsrFetchResponse?> AuthorizeAsync(
		IServiceProvider services,
		HttpContext? httpContext,
		RemoteServiceDescriptor service,
		RemoteMethodDescriptor method)
	{
		if (method.AllowAnonymous || service.AllowAnonymous) return null;

		var requirements = Requirements(service, method).ToArray();
		if (requirements.Length == 0) return null;

		// A render with no HttpContext (e.g. a warm-up render) has no user to authorize,
		// so a protected query must not run.
		if (httpContext is null) return new SsrFetchResponse(401, Problem(401, "Unauthorized."));

		var policyProvider = services.GetService<IAuthorizationPolicyProvider>()
			?? throw new InvalidOperationException(
				$"Remote service '{service.Name}' is protected with [Authorize], but authorization services are not " +
				"registered. Call builder.Services.AddAuthorization() (and AddAuthentication) during startup.");

		var policy = await AuthorizationPolicy.CombineAsync(
			policyProvider,
			requirements.Select(r => new AuthorizeAttribute
			{
				Policy = r.Policy,
				Roles = r.Roles,
				AuthenticationSchemes = r.AuthenticationSchemes
			})).ConfigureAwait(false);
		if (policy is null) return null;

		var authorizationService = services.GetRequiredService<IAuthorizationService>();
		var result = await authorizationService.AuthorizeAsync(httpContext.User, resource: httpContext, policy)
			.ConfigureAwait(false);
		if (result.Succeeded) return null;

		// Same distinction the authorization middleware makes: unauthenticated users get a
		// challenge (401), authenticated-but-not-permitted users get a forbid (403).
		var authenticated = httpContext.User.Identity?.IsAuthenticated == true;
		return authenticated
			? new SsrFetchResponse(403, Problem(403, "Forbidden."))
			: new SsrFetchResponse(401, Problem(401, "Unauthorized."));
	}

	private static IEnumerable<RemoteAuthorization> Requirements(
		RemoteServiceDescriptor service,
		RemoteMethodDescriptor method) =>
		(service.Authorization ?? []).Concat(method.Authorization ?? []);

	private static string Problem(int status, string title) =>
		JsonSerializer.Serialize(new { title, status }, SvelteJson.Options);
}
