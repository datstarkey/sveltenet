namespace SvelteNet.Core.Tests;

/// <summary>
/// Exercises the Jint engine against a hand-written ESM bundle shaped like Vite SSR
/// output (default component export + render helper + shared chunk import).
/// </summary>
public class JintSsrEngineTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("sveltenet-jint-").FullName;
	private readonly JintSsrEngine _engine;

	public JintSsrEngineTests()
	{
		var serverDir = Path.Combine(_root, ".svelte-net", "server");
		Directory.CreateDirectory(serverDir);

		File.WriteAllText(Path.Combine(serverDir, "render.js"), """
		export function renderComponent(component, props) {
			return component(props);
		}
		""");

		File.WriteAllText(Path.Combine(serverDir, "chunk.js"), """
		export const paragraph = (text) => `<p>${text}</p>`;
		""");

		File.WriteAllText(Path.Combine(serverDir, "Index.js"), """
		import { paragraph } from './chunk.js';

		export default (props) => ({
			head: `<title>${props?.data?.title ?? 'untitled'}</title>`,
			body: paragraph(props?.data?.title ?? 'no props')
		});
		""");

		_engine = new JintSsrEngine(new SvelteOptions { ContentRoot = _root });
	}

	public void Dispose()
	{
		_engine.Dispose();
		Directory.Delete(_root, recursive: true);
	}

	[Fact]
	public async Task Renders_with_props()
	{
		var result = await _engine.RenderAsync("Index.js", "render.js", "{\"data\":{\"title\":\"hello\"}}");

		Assert.Equal("<title>hello</title>", result.Head);
		Assert.Equal("<p>hello</p>", result.Body);
	}

	[Fact]
	public async Task Renders_without_props()
	{
		var result = await _engine.RenderAsync("Index.js", "render.js", null);

		Assert.Equal("<title>untitled</title>", result.Head);
		Assert.Equal("<p>no props</p>", result.Body);
	}

	private sealed class FakeFetchHandler : ISvelteSsrFetchHandler
	{
		private readonly TimeSpan _delay;

		public FakeFetchHandler(TimeSpan delay = default) => _delay = delay;

		public List<string> Urls { get; } = [];

		public async ValueTask<SsrFetchResponse> HandleAsync(string url, string method, CancellationToken cancellationToken = default)
		{
			lock (Urls) Urls.Add($"{method} {url}");
			if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken);
			return SsrFetchResponse.Json("{\"value\":\"from-bridge\"}");
		}
	}

	[Fact]
	public async Task The_fetch_bridge_resolves_awaits_during_ssr()
	{
		WriteAsyncComponent();
		var handler = new FakeFetchHandler();
		using var engine = new JintSsrEngine(new SvelteOptions { ContentRoot = _root }, handler);

		var result = await engine.RenderAsync("Async.js", "render.js", null);

		Assert.Equal("<p>from-bridge</p>", result.Body);
		Assert.Equal("GET /_sveltenet/remote/Api/GetThing", Assert.Single(handler.Urls));
	}

	/// <summary>
	/// Jint cannot suspend a running script, so the bridge blocks its worker thread. The
	/// engine must keep that off the caller: many concurrent renders that each await a
	/// genuinely asynchronous bridge call have to complete, not deadlock.
	/// </summary>
	[Fact]
	public async Task Concurrent_renders_awaiting_the_bridge_do_not_starve_the_caller()
	{
		WriteAsyncComponent();
		var handler = new FakeFetchHandler(TimeSpan.FromMilliseconds(25));
		using var engine = new JintSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			handler,
			new JintSsrOptions { MaxConcurrentRenders = 4 });

		var renders = Enumerable.Range(0, 32)
			.Select(_ => engine.RenderAsync("Async.js", "render.js", null).AsTask())
			.ToArray();

		var completed = await Task.WhenAny(Task.WhenAll(renders), Task.Delay(TimeSpan.FromSeconds(30)));
		Assert.True(completed is Task<SsrResult[]>, "Concurrent SSR renders deadlocked or exceeded 30s.");

		foreach (var render in renders) Assert.Equal("<p>from-bridge</p>", (await render).Body);
		Assert.Equal(32, handler.Urls.Count);
	}

	/// <summary>
	/// Engines outlive one request, so the fetch shim must read the token of the render
	/// currently running rather than the one the engine was first created with.
	/// </summary>
	[Fact]
	public async Task A_pooled_engine_uses_the_current_requests_cancellation_token()
	{
		WriteAsyncComponent();
		var handler = new FakeFetchHandler(TimeSpan.FromSeconds(30));
		using var engine = new JintSsrEngine(new SvelteOptions { ContentRoot = _root }, handler);

		// Warm the pool with a completed render so the next one reuses that engine.
		var quick = new FakeFetchHandler();
		using (var warm = new JintSsrEngine(new SvelteOptions { ContentRoot = _root }, quick))
			await warm.RenderAsync("Async.js", "render.js", null);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await engine.RenderAsync("Async.js", "render.js", null, cts.Token));
	}

	[Fact]
	public async Task Pooled_engines_render_concurrently_with_correct_results()
	{
		var renders = Enumerable.Range(0, 50).Select(async i =>
		{
			var result = await _engine.RenderAsync("Index.js", "render.js", $"{{\"data\":{{\"title\":\"run-{i}\"}}}}");
			Assert.Equal($"<p>run-{i}</p>", result.Body);
		});

		await Task.WhenAll(renders);
	}

	[Fact]
	public async Task Runaway_ssr_is_stopped_by_the_configured_timeout()
	{
		File.WriteAllText(Path.Combine(_root, ".svelte-net", "server", "Loop.js"), """
		export default () => {
			while (true) {}
		};
		""");
		using var engine = new JintSsrEngine(
			new SvelteOptions { ContentRoot = _root },
			ssrOptions: new JintSsrOptions { Timeout = TimeSpan.FromMilliseconds(50) });

		await Assert.ThrowsAnyAsync<Exception>(async () => await engine.RenderAsync("Loop.js", "render.js", null));
	}

	private void WriteAsyncComponent()
	{
		File.WriteAllText(Path.Combine(_root, ".svelte-net", "server", "Async.js"), """
		export default async (props) => {
			const response = await fetch('/_sveltenet/remote/Api/GetThing');
			const data = await response.json();
			return { head: '', body: `<p>${data.value}</p>` };
		};
		""");
	}
}
