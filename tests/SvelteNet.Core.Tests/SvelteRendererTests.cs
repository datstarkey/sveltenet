namespace SvelteNet.Core.Tests;

using SvelteNet.Core.Tests.Fixtures;

public class SvelteRendererTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("sveltenet-renderer-").FullName;

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private SvelteOptions Options(bool isDev = false) => new() { ContentRoot = _root, IsDev = isDev };

	private sealed class FakeSsrEngine : ISvelteSsrEngine
	{
		public int Calls;
		public (string Component, string Render, string? Props)? LastCall;

		public ValueTask<SsrResult> RenderAsync(string componentModule, string renderModule, string? propsJson, CancellationToken cancellationToken = default)
		{
			Interlocked.Increment(ref Calls);
			LastCall = (componentModule, renderModule, propsJson);
			return ValueTask.FromResult(new SsrResult { Head = "<title>ssr-head</title>", Body = "<p>ssr-body</p>" });
		}
	}

	private void WriteClientManifest()
	{
		var dir = Path.Combine(_root, "wwwroot", "client", ".vite");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "manifest.json"), """
		{
			"../../packages/sveltenet/src/client.js": { "file": "assets/mount-abc.js", "name": "sveltenet-client", "isEntry": true },
			"Svelte/Index.svelte": {
				"file": "assets/Index-abc.js",
				"isEntry": true,
				"imports": ["_shared-x.js"],
				"css": ["assets/Index-abc.css"]
			},
			"_shared-x.js": { "file": "assets/shared-x.js", "css": ["assets/shared-x.css"] },
			"Svelte/Admin/Users.svelte": { "file": "assets/Users-abc.js", "isEntry": true }
		}
		""");
	}

	private void WriteServerManifest()
	{
		var dir = Path.Combine(_root, ".svelte-net", "server", ".vite");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "manifest.json"), """
		{
			"../../packages/sveltenet/src/server.js": { "file": "render.js", "name": "sveltenet-ssr", "isEntry": true },
			"Svelte/Index.svelte": { "file": "Index.js", "isEntry": true },
			"Svelte/Admin/Users.svelte": { "file": "Users.js", "isEntry": true }
		}
		""");
	}

	[Fact]
	public async Task Dev_mode_loads_from_the_vite_dev_server_and_skips_ssr()
	{
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(isDev: true), engine);

		var result = await renderer.RenderAsync("Index", new { Title = "hi" });

		Assert.Contains("http://localhost:5173/@vite/client", result.Html);
		Assert.Contains("http://localhost:5173/@id/sveltenet/client", result.Html);
		Assert.Contains("http://localhost:5173/Svelte/Index.svelte", result.Html);
		Assert.Contains("hydrate: false", result.Html);
		Assert.Contains("<div id=\"svelte-index\"></div>", result.Html);
		Assert.Equal(0, engine.Calls);
		Assert.Equal(string.Empty, result.Head);
	}

	[Fact]
	public async Task Prod_mode_resolves_hashed_files_and_hydrates_ssr_output()
	{
		WriteClientManifest();
		WriteServerManifest();
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(), engine);

		var result = await renderer.RenderAsync("Index", new { Title = "hi" });

		Assert.Contains("/client/assets/mount-abc.js", result.Html);
		Assert.Contains("/client/assets/Index-abc.js", result.Html);
		Assert.Contains("hydrate: true", result.Html);
		Assert.Contains("<div id=\"svelte-index\"><p>ssr-body</p></div>", result.Html);
		Assert.Equal(1, engine.Calls);
		Assert.Equal(("Index.js", "render.js", "{\"data\":{\"title\":\"hi\"}}"), engine.LastCall);
		Assert.Contains("<title>ssr-head</title>", result.Head);
	}

	[Fact]
	public async Task Prod_mode_is_client_only_when_no_ssr_engine_is_registered()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options());

		var result = await renderer.RenderAsync("Index", new { Title = "hi" });

		Assert.Contains("hydrate: false", result.Html);
		Assert.Contains("<div id=\"svelte-index\"></div>", result.Html);
		Assert.DoesNotContain("ssr-head", result.Head);
	}

	/// <summary>
	/// A server render can await remote queries; there is no correct way to do that from
	/// a synchronous call, so the sync API refuses rather than blocking a request thread.
	/// </summary>
	[Fact]
	public void Synchronous_render_refuses_when_ssr_is_active()
	{
		WriteClientManifest();
		WriteServerManifest();
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(), engine);

		var ex = Assert.Throws<InvalidOperationException>(() => renderer.Render("Index"));

		Assert.Contains("RenderAsync", ex.Message);
		Assert.Equal(0, engine.Calls);
	}

	[Fact]
	public void Synchronous_render_is_allowed_when_ssr_is_off()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = renderer.Render(new ComponentOptions { Component = "Index", Ssr = false });

		Assert.Contains("hydrate: false", result.Html);
	}

	[Fact]
	public async Task Css_links_include_imported_chunks_recursively()
	{
		WriteClientManifest();
		WriteServerManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = await renderer.RenderAsync("Index");

		Assert.Contains("<link rel=\"stylesheet\" href=\"/client/assets/Index-abc.css\" />", result.Head);
		Assert.Contains("<link rel=\"stylesheet\" href=\"/client/assets/shared-x.css\" />", result.Head);
	}

	[Fact]
	public async Task Props_are_camel_cased_including_enums_and_dictionary_keys()
	{
		var renderer = new SvelteRenderer(Options(isDev: true), new FakeSsrEngine());

		var result = await renderer.RenderAsync("Index", new
		{
			UserName = "jake",
			Status = OrderStatus.PendingApproval,
			ModelState = new Dictionary<string, string[]> { ["NewLabel"] = ["required"] }
		});

		Assert.Contains("\"userName\":\"jake\"", result.Html);
		Assert.Contains("\"status\":\"pendingApproval\"", result.Html);
		Assert.Contains("\"newLabel\":[\"required\"]", result.Html);
	}

	[Fact]
	public async Task Props_cannot_break_out_of_the_inline_script()
	{
		var renderer = new SvelteRenderer(Options(isDev: true), new FakeSsrEngine());

		var result = await renderer.RenderAsync("Index", new { Payload = "</script><script>alert(1)</script>" });

		Assert.DoesNotContain("</script><script>alert(1)", result.Html);
	}

	[Fact]
	public async Task Element_ids_are_slugs_of_the_component_path()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = await renderer.RenderAsync(new ComponentOptions { Component = "Admin/Users", Ssr = false });

		Assert.Contains("<div id=\"svelte-admin-users\">", result.Html);
	}

	[Fact]
	public async Task Element_ids_are_encoded_for_html_and_javascript_contexts()
	{
		var renderer = new SvelteRenderer(Options(isDev: true), new FakeSsrEngine());

		var result = await renderer.RenderAsync(new ComponentOptions
		{
			Component = "Index",
			ElementId = "\"><script>alert(1)</script>"
		});

		Assert.Contains("id=\"&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;\"", result.Html);
		Assert.Contains("document.getElementById(\"\\u0022\\u003E\\u003Cscript\\u003Ealert(1)\\u003C/script\\u003E\")", result.Html);
		Assert.DoesNotContain("<script>alert(1)</script>", result.Html);
	}

	[Fact]
	public async Task Csr_can_be_disabled_per_component()
	{
		WriteClientManifest();
		WriteServerManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = await renderer.RenderAsync(new ComponentOptions { Component = "Index", Csr = false });

		Assert.DoesNotContain("<script", result.Html);
		Assert.Contains("ssr-body", result.Html);
	}

	[Fact]
	public async Task Ssr_can_be_disabled_per_component()
	{
		WriteClientManifest();
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(), engine);

		var result = await renderer.RenderAsync(new ComponentOptions { Component = "Index", Ssr = false });

		Assert.Equal(0, engine.Calls);
		Assert.Contains("<div id=\"svelte-index\"></div>", result.Html);
	}

	[Fact]
	public async Task Missing_component_in_manifest_throws_a_helpful_error()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			renderer.RenderAsync(new ComponentOptions { Component = "Missing", Ssr = false }));

		Assert.Contains("Svelte/Missing.svelte", ex.Message);
	}

	[Fact]
	public async Task Missing_manifest_throws_a_helpful_error()
	{
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
			renderer.RenderAsync(new ComponentOptions { Component = "Index", Ssr = false }));

		Assert.Contains("manifest", ex.Message);
	}

	/// <summary>
	/// Manifest keys are file paths. On a case-sensitive file system 'index.svelte' and
	/// 'Index.svelte' are different components, so the lookup must not fold case.
	/// </summary>
	[Fact]
	public async Task Manifest_lookup_is_case_sensitive()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			renderer.RenderAsync(new ComponentOptions { Component = "index", Ssr = false }));

		Assert.Contains("Svelte/index.svelte", ex.Message);
	}
}
