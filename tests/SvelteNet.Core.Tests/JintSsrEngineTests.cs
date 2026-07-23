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
