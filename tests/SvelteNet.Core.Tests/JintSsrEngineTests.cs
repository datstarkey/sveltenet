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
		var serverDir = Path.Combine(_root, "svelte-ssr");
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

	public void Dispose() => Directory.Delete(_root, recursive: true);

	[Fact]
	public void Renders_with_props()
	{
		var result = _engine.Render("Index.js", "render.js", "{\"data\":{\"title\":\"hello\"}}");

		Assert.Equal("<title>hello</title>", result.Head);
		Assert.Equal("<p>hello</p>", result.Body);
	}

	[Fact]
	public void Renders_without_props()
	{
		var result = _engine.Render("Index.js", "render.js", null);

		Assert.Equal("<title>untitled</title>", result.Head);
		Assert.Equal("<p>no props</p>", result.Body);
	}

	private sealed class FakeFetchHandler : ISvelteSsrFetchHandler
	{
		public List<string> Urls { get; } = [];

		public (int Status, string? Body) Handle(string url, string method)
		{
			Urls.Add($"{method} {url}");
			return (200, "{\"value\":\"from-bridge\"}");
		}
	}

	[Fact]
	public void The_fetch_bridge_resolves_awaits_during_ssr()
	{
		File.WriteAllText(Path.Combine(_root, "svelte-ssr", "Async.js"), """
		export default async (props) => {
			const response = await fetch('/_sveltenet/remote/Api/GetThing');
			const data = await response.json();
			return { head: '', body: `<p>${data.value}</p>` };
		};
		""");
		var handler = new FakeFetchHandler();
		var engine = new JintSsrEngine(new SvelteOptions { ContentRoot = _root }, handler);

		var result = engine.Render("Async.js", "render.js", null);

		Assert.Equal("<p>from-bridge</p>", result.Body);
		Assert.Equal("GET /_sveltenet/remote/Api/GetThing", Assert.Single(handler.Urls));
	}

	[Fact]
	public void Pooled_engines_render_concurrently_with_correct_results()
	{
		Parallel.For(0, 50, i =>
		{
			var result = _engine.Render("Index.js", "render.js", $"{{\"data\":{{\"title\":\"run-{i}\"}}}}");
			Assert.Equal($"<p>run-{i}</p>", result.Body);
		});
	}
}
