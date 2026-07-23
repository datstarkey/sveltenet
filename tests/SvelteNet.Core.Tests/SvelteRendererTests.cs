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

		public SsrResult Render(string componentModule, string renderModule, string? propsJson)
		{
			Calls++;
			LastCall = (componentModule, renderModule, propsJson);
			return new SsrResult { Head = "<title>ssr-head</title>", Body = "<p>ssr-body</p>" };
		}
	}

	private void WriteClientManifest()
	{
		var dir = Path.Combine(_root, "wwwroot", "client", ".vite");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "manifest.json"), """
		{
			"Svelte/mount.ts": { "file": "assets/mount-abc.js", "isEntry": true },
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
		var dir = Path.Combine(_root, "svelte-ssr", ".vite");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "manifest.json"), """
		{
			"Svelte/render.ts": { "file": "render.js", "isEntry": true },
			"Svelte/Index.svelte": { "file": "Index.js", "isEntry": true },
			"Svelte/Admin/Users.svelte": { "file": "Users.js", "isEntry": true }
		}
		""");
	}

	[Fact]
	public void Dev_mode_loads_from_the_vite_dev_server_and_skips_ssr()
	{
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(isDev: true), engine);

		var result = renderer.Render("Index", new { Title = "hi" });

		Assert.Contains("http://localhost:5173/@vite/client", result.Html);
		Assert.Contains("http://localhost:5173/Svelte/mount.ts", result.Html);
		Assert.Contains("http://localhost:5173/Svelte/Index.svelte", result.Html);
		Assert.Contains("hydrate: false", result.Html);
		Assert.Contains("<div id=\"svelte-index\"></div>", result.Html);
		Assert.Equal(0, engine.Calls);
		Assert.Equal(string.Empty, result.Head);
	}

	[Fact]
	public void Prod_mode_resolves_hashed_files_and_hydrates_ssr_output()
	{
		WriteClientManifest();
		WriteServerManifest();
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(), engine);

		var result = renderer.Render("Index", new { Title = "hi" });

		Assert.Contains("/client/assets/mount-abc.js", result.Html);
		Assert.Contains("/client/assets/Index-abc.js", result.Html);
		Assert.Contains("hydrate: true", result.Html);
		Assert.Contains("<div id=\"svelte-index\"><p>ssr-body</p></div>", result.Html);
		Assert.Equal(1, engine.Calls);
		Assert.Equal(("Index.js", "render.js", "{\"data\":{\"title\":\"hi\"}}"), engine.LastCall);
		Assert.Contains("<title>ssr-head</title>", result.Head);
	}

	[Fact]
	public void Css_links_include_imported_chunks_recursively()
	{
		WriteClientManifest();
		WriteServerManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = renderer.Render("Index");

		Assert.Contains("<link rel=\"stylesheet\" href=\"/client/assets/Index-abc.css\" />", result.Head);
		Assert.Contains("<link rel=\"stylesheet\" href=\"/client/assets/shared-x.css\" />", result.Head);
	}

	[Fact]
	public void Props_are_camel_cased_including_enums_and_dictionary_keys()
	{
		var renderer = new SvelteRenderer(Options(isDev: true), new FakeSsrEngine());

		var result = renderer.Render("Index", new
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
	public void Props_cannot_break_out_of_the_inline_script()
	{
		var renderer = new SvelteRenderer(Options(isDev: true), new FakeSsrEngine());

		var result = renderer.Render("Index", new { Payload = "</script><script>alert(1)</script>" });

		Assert.DoesNotContain("</script><script>alert(1)", result.Html);
	}

	[Fact]
	public void Element_ids_are_slugs_of_the_component_path()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = renderer.Render(new ComponentOptions { Component = "Admin/Users", Ssr = false });

		Assert.Contains("<div id=\"svelte-admin-users\">", result.Html);
	}

	[Fact]
	public void Csr_can_be_disabled_per_component()
	{
		WriteClientManifest();
		WriteServerManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var result = renderer.Render(new ComponentOptions { Component = "Index", Csr = false });

		Assert.DoesNotContain("<script", result.Html);
		Assert.Contains("ssr-body", result.Html);
	}

	[Fact]
	public void Ssr_can_be_disabled_per_component()
	{
		WriteClientManifest();
		var engine = new FakeSsrEngine();
		var renderer = new SvelteRenderer(Options(), engine);

		var result = renderer.Render(new ComponentOptions { Component = "Index", Ssr = false });

		Assert.Equal(0, engine.Calls);
		Assert.Contains("<div id=\"svelte-index\"></div>", result.Html);
	}

	[Fact]
	public void Missing_component_in_manifest_throws_a_helpful_error()
	{
		WriteClientManifest();
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var ex = Assert.Throws<InvalidOperationException>(() =>
			renderer.Render(new ComponentOptions { Component = "Missing", Ssr = false }));

		Assert.Contains("Svelte/Missing.svelte", ex.Message);
	}

	[Fact]
	public void Missing_manifest_throws_a_helpful_error()
	{
		var renderer = new SvelteRenderer(Options(), new FakeSsrEngine());

		var ex = Assert.Throws<FileNotFoundException>(() =>
			renderer.Render(new ComponentOptions { Component = "Index", Ssr = false }));

		Assert.Contains("manifest", ex.Message);
	}
}
