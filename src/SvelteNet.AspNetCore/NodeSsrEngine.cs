namespace SvelteNet.AspNetCore;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;

/// <summary>Runs each server render in the installed Node.js runtime.</summary>
public sealed class NodeSsrEngine : CliSsrEngine
{
	public NodeSsrEngine(
		SvelteOptions options,
		NodeSsrOptions ssrOptions,
		IHttpContextAccessor? httpContextAccessor = null,
		IServer? server = null)
		: base(
			options,
			httpContextAccessor,
			server,
			ssrOptions.ExecutablePath,
			ssrOptions.Timeout,
			"Node.js",
			ssrOptions.BaseUrl,
			ssrOptions.ForwardHeaders,
			"--input-type=module",
			"--eval")
	{
	}
}
