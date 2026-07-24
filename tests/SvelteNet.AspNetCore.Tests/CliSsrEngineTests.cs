namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

public class CliSsrEngineTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("sveltenet-cli-ssr-").FullName;

	public void Dispose() => Directory.Delete(_root, recursive: true);

	[Fact]
	public void Missing_node_cli_has_an_actionable_error()
	{
		var exception = Assert.Throws<InvalidOperationException>(() => new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions { ExecutablePath = Path.Combine(_root, "missing-node") }));

		Assert.Contains("requires", exception.Message);
		Assert.Contains("missing-node", exception.Message);
		Assert.Contains("PATH", exception.Message);
	}

	[Fact]
	public void Node_renderer_imports_built_modules_and_returns_the_result()
	{
		WriteModules();
		var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions());

		var result = engine.Render(
			"component.mjs",
			"render.mjs",
			"""{"message":"rendered by node"}""");

		Assert.Equal("<title>Example</title>", result.Head);
		Assert.Equal("<p>rendered by node</p>", result.Body);
	}

	[Fact]
	public void Bun_renderer_imports_built_modules_and_returns_the_result_when_bun_is_available()
	{
		if (!CanRun("bun")) return;

		WriteModules();
		var engine = new BunJsSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new BunJsSsrOptions());

		var result = engine.Render(
			"component.mjs",
			"render.mjs",
			"""{"message":"rendered by bun"}""");

		Assert.Equal("<title>Example</title>", result.Head);
		Assert.Equal("<p>rendered by bun</p>", result.Body);
	}

	[Fact]
	public void Renderer_rejects_modules_outside_the_server_output()
	{
		var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions());

		var exception = Assert.Throws<InvalidOperationException>(() =>
			engine.Render("../outside.mjs", "render.mjs", null));

		Assert.Contains("outside the server output", exception.Message);
	}

	[Fact]
	public void Loopback_base_url_comes_from_the_server_and_normalizes_wildcards()
	{
		using var server = new FakeServer("http://0.0.0.0:5179");

		var result = CliSsrEngine.ResolveBaseUrl(null, server);

		Assert.Equal(new Uri("http://127.0.0.1:5179/"), result);
	}

	[Fact]
	public void Explicit_trusted_base_url_wins_over_the_server_address()
	{
		using var server = new FakeServer("http://127.0.0.1:5179");

		var result = CliSsrEngine.ResolveBaseUrl(new Uri("https://internal.example.test/app"), server);

		Assert.Equal(new Uri("https://internal.example.test/app/"), result);
	}

	private void WriteModules()
	{
		var server = Path.Combine(_root, ".svelte-net", "server");
		Directory.CreateDirectory(server);
		File.WriteAllText(Path.Combine(server, "component.mjs"), "export default { name: 'Example' };");
		File.WriteAllText(
			Path.Combine(server, "render.mjs"),
			"""
			export async function renderComponent(component, props) {
				return {
					head: `<title>${component.name}</title>`,
					body: `<p>${props.message}</p>`
				};
			}
			""");
	}

	private static bool CanRun(string executable)
	{
		try
		{
			using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = executable,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				ArgumentList = { "--version" }
			});
			process?.WaitForExit(5_000);
			return process?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private sealed class FakeServer : IServer
	{
		public FakeServer(string address)
		{
			var addresses = new ServerAddressesFeature();
			addresses.Addresses.Add(address);
			Features.Set<IServerAddressesFeature>(addresses);
		}

		public IFeatureCollection Features { get; } = new FeatureCollection();

		public void Dispose()
		{
		}

		public Task StartAsync<TContext>(
			IHttpApplication<TContext> application,
			CancellationToken cancellationToken) where TContext : notnull =>
			Task.CompletedTask;

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
