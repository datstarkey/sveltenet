namespace SvelteNet.AspNetCore.Tests;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

public class CliSsrEngineTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("sveltenet-cli-ssr-").FullName;

	public void Dispose() => Directory.Delete(_root, recursive: true);

	/// <summary>
	/// The CLI is probed on first render rather than in the constructor — resolving a
	/// singleton from DI should not spawn a process — so the actionable error surfaces here.
	/// </summary>
	[Fact]
	public async Task Missing_node_cli_has_an_actionable_error()
	{
		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions { ExecutablePath = Path.Combine(_root, "missing-node") });

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await engine.RenderAsync("component.mjs", "render.mjs", null));

		Assert.Contains("requires", exception.Message);
		Assert.Contains("missing-node", exception.Message);
		Assert.Contains("PATH", exception.Message);
	}

	[Fact]
	public void Constructing_the_engine_does_not_touch_the_cli()
	{
		// A bad executable path must not throw until something actually renders.
		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions { ExecutablePath = Path.Combine(_root, "missing-node") });

		Assert.NotNull(engine);
	}

	[Fact]
	public async Task Node_renderer_imports_built_modules_and_returns_the_result()
	{
		WriteModules();
		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions());

		var result = await engine.RenderAsync(
			"component.mjs",
			"render.mjs",
			"""{"message":"rendered by node"}""");

		Assert.Equal("<title>Example</title>", result.Head);
		Assert.Equal("<p>rendered by node</p>", result.Body);
	}

	[Fact]
	public async Task Bun_renderer_imports_built_modules_and_returns_the_result_when_bun_is_available()
	{
		if (!CanRun("bun")) return;

		WriteModules();
		using var engine = new BunJsSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new BunJsSsrOptions());

		var result = await engine.RenderAsync(
			"component.mjs",
			"render.mjs",
			"""{"message":"rendered by bun"}""");

		Assert.Equal("<title>Example</title>", result.Head);
		Assert.Equal("<p>rendered by bun</p>", result.Body);
	}

	[Fact]
	public async Task Renderer_rejects_modules_outside_the_server_output()
	{
		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await engine.RenderAsync("../outside.mjs", "render.mjs", null));

		Assert.Contains("outside the server output", exception.Message);
	}

	/// <summary>Concurrent renders each get their own process and must not interleave output.</summary>
	[Fact]
	public async Task Concurrent_node_renders_return_their_own_results()
	{
		WriteModules();
		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions());

		var renders = Enumerable.Range(0, 8).Select(async i =>
		{
			var result = await engine.RenderAsync("component.mjs", "render.mjs", $$"""{"message":"run-{{i}}"}""");
			Assert.Equal($"<p>run-{i}</p>", result.Body);
		});

		await Task.WhenAll(renders);
	}

	/// <summary>
	/// HeadersInit has three legal forms. The runner used Object.entries(), which yields
	/// nothing for a Headers instance or an array of pairs — so component code passing
	/// either silently lost every header it set.
	/// </summary>
	[Theory]
	[InlineData("new Headers({ 'X-Probe': 'from-headers' })", "from-headers")]
	[InlineData("[['X-Probe', 'from-array']]", "from-array")]
	[InlineData("{ 'X-Probe': 'from-object' }", "from-object")]
	public async Task The_node_runner_forwards_every_headers_init_form(string headersExpression, string expected)
	{
		// The shim wraps globalThis.fetch before the component runs, so the only way to see
		// what it produced is to receive the request. A real loopback listener does that.
		using var listener = new System.Net.HttpListener();
		var port = FreePort();
		listener.Prefixes.Add($"http://127.0.0.1:{port}/");
		listener.Start();

		var received = Task.Run(async () =>
		{
			var context = await listener.GetContextAsync();
			var probe = context.Request.Headers["X-Probe"] ?? "missing";
			context.Response.StatusCode = 200;
			context.Response.Close();
			return probe;
		});

		var server = Path.Combine(_root, ".svelte-net", "server");
		Directory.CreateDirectory(server);
		File.WriteAllText(Path.Combine(server, "component.mjs"), "export default { name: 'Example' };");
		File.WriteAllText(Path.Combine(server, "render.mjs"), $$"""
			export async function renderComponent() {
				await fetch('/probe', { headers: {{headersExpression}} });
				return { head: '', body: 'done' };
			}
			""");

		using var engine = new NodeSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			new NodeSsrOptions { BaseUrl = new Uri($"http://127.0.0.1:{port}") });

		await engine.RenderAsync("component.mjs", "render.mjs", null);

		Assert.Equal(expected, await received.WaitAsync(TimeSpan.FromSeconds(30)));
	}

	private static int FreePort()
	{
		using var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		socket.Start();
		var port = ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
		socket.Stop();
		return port;
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
