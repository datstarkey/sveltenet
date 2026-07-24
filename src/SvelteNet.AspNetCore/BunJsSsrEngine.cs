namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;

/// <summary>Runs each server render in the installed Bun runtime.</summary>
public sealed class BunJsSsrEngine : CliSsrEngine
{
	public BunJsSsrEngine(
		SvelteOptions options,
		BunJsSsrOptions ssrOptions,
		IHttpContextAccessor? httpContextAccessor = null,
		IServer? server = null)
		: base(
			options,
			httpContextAccessor,
			server,
			ssrOptions.ExecutablePath,
			ssrOptions.Timeout,
			"Bun",
			ssrOptions.BaseUrl,
			ssrOptions.ForwardHeaders,
			"--eval")
	{
	}
}
