namespace SvelteNet.AspNetCore.Tests;

using SvelteNet.AspNetCore.Dev;
using SvelteNet.AspNetCore.Tests.Fixtures;
using SvelteNet.AspNetCore.Tests.Fixtures.Pages;
using SvelteNet.AspNetCore.Tests.Fixtures.Pages.Admin;

public class SvelteScaffolderTests : IDisposable
{
	private readonly string _root = Directory.CreateTempSubdirectory("sveltenet-scaffold-").FullName;
	private readonly SvelteOptions _options;

	public SvelteScaffolderTests()
	{
		_options = new SvelteOptions { ContentRoot = _root };
	}

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private void Run() => SvelteScaffolder.Run(_options, [typeof(HomeModel), typeof(UsersModel)], [typeof(CardViewModel)]);

	private string ReadSvelteFile(params string[] segments) =>
		File.ReadAllText(Path.Combine([_root, "Svelte", .. segments]));

	[Fact]
	public void Generates_shared_types_for_all_referenced_models()
	{
		Run();

		var types = ReadSvelteFile("types.ts");
		Assert.Contains("export interface Widget", types);
		Assert.Contains("export type WidgetKind = 'basic' | 'fancy';", types);
		Assert.Contains("export interface Paged<T>", types);
	}

	[Fact]
	public void Generates_a_typed_data_interface_per_page()
	{
		Run();

		var types = ReadSvelteFile("Home.types.ts");
		Assert.Contains("export interface HomeData", types);
		Assert.Contains("title: string;", types);
		Assert.Contains("widgets: Widget[];", types);
		Assert.Contains("modelState: Record<string, string[]>;", types);
		Assert.Contains("antiforgeryToken: string;", types);
		Assert.Contains("import type { Widget } from './types';", types);
	}

	[Fact]
	public void Nested_pages_mirror_the_folder_structure_with_relative_imports()
	{
		Run();

		var types = ReadSvelteFile("Admin", "Users.types.ts");
		Assert.Contains("export interface UsersData", types);
		Assert.Contains("users: Paged<Widget>;", types);
		Assert.Contains("import type { Paged, Widget } from '../types';", types);
	}

	[Fact]
	public void Scaffolds_a_runes_mode_component_when_missing()
	{
		Run();

		var component = ReadSvelteFile("Home.svelte");
		Assert.Contains("import type { HomeData } from './Home.types';", component);
		Assert.Contains("let { data }: { data: HomeData } = $props();", component);
	}

	[Fact]
	public void Never_overwrites_user_owned_files()
	{
		Run();
		var componentPath = Path.Combine(_root, "Svelte", "Home.svelte");
		File.WriteAllText(componentPath, "<h1>mine now</h1>");

		Run();

		Assert.Equal("<h1>mine now</h1>", File.ReadAllText(componentPath));
	}

	[Fact]
	public void Regenerates_type_files_every_run()
	{
		Run();
		var typesPath = Path.Combine(_root, "Svelte", "Home.types.ts");
		File.WriteAllText(typesPath, "// stale");

		Run();

		Assert.DoesNotContain("stale", File.ReadAllText(typesPath));
	}

	[Fact]
	public void Scaffolds_runtime_helpers_and_vite_config()
	{
		Run();

		Assert.Contains("export { mountComponent } from 'sveltenet/client';", ReadSvelteFile("mount.ts"));
		Assert.Contains("export { renderComponent } from 'sveltenet/server';", ReadSvelteFile("render.ts"));
		var viteConfig = File.ReadAllText(Path.Combine(_root, "vite.config.ts"));
		Assert.Contains("import { sveltenet } from 'sveltenet/vite';", viteConfig);
		Assert.Contains("plugins: [sveltenet()]", viteConfig);
		Assert.Contains("\"sveltenet\":", File.ReadAllText(Path.Combine(_root, "package.json")));
	}

	[Fact]
	public void Vite_config_forwards_non_default_paths_to_the_plugin()
	{
		_options.PagesPath = "Islands";
		Run();

		var viteConfig = File.ReadAllText(Path.Combine(_root, "vite.config.ts"));
		Assert.Contains("pagesPath: 'Islands'", viteConfig);
		Assert.Contains("serverOutDir: 'svelte-ssr'", viteConfig);
	}

	[Fact]
	public void Component_models_get_types_and_a_scaffolded_component()
	{
		Run();

		Assert.Contains("export interface CardViewModel", ReadSvelteFile("types.ts"));

		var component = ReadSvelteFile("Components", "Card.svelte");
		Assert.Contains("import type { CardViewModel } from '../types';", component);
		Assert.Contains("let { data }: { data: CardViewModel } = $props();", component);
	}

	[Fact]
	public void Generates_route_ids_from_cshtml_files()
	{
		var pages = Path.Combine(_root, "Pages");
		Directory.CreateDirectory(Path.Combine(pages, "Admin"));
		File.WriteAllText(Path.Combine(pages, "Index.cshtml"), "@page");
		File.WriteAllText(Path.Combine(pages, "Admin", "Users.cshtml"), "@page");
		File.WriteAllText(Path.Combine(pages, "_Layout.cshtml"), "layout");

		Run();

		var routes = ReadSvelteFile("routes.d.ts");
		Assert.Contains("\"/\"", routes);
		Assert.Contains("\"/Admin/Users\"", routes);
		Assert.DoesNotContain("_Layout", routes);
	}
}
