namespace SvelteNet.AspNetCore.Tests;

using SvelteNet.AspNetCore.Dev;
using SvelteNet.AspNetCore.Tests.Fixtures;
using SvelteNet.AspNetCore.Tests.Fixtures.Features.Inventory;
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

	private void Run() => SvelteScaffolder.Run(_options, [typeof(HomeModel), typeof(UsersModel)], [typeof(CardViewModel)], [typeof(WidgetApi)]);

	private string ReadSvelteFile(params string[] segments) =>
		File.ReadAllText(Path.Combine([_root, "Svelte", .. segments]));

	private string ReadGeneratedFile(params string[] segments) =>
		File.ReadAllText(Path.Combine([_root, ".svelte-net", "types", .. segments]));

	[Fact]
	public void Generates_shared_types_for_all_referenced_models()
	{
		Run();

		var types = ReadGeneratedFile("models.d.ts");
		Assert.Contains("interface Widget", types);
		Assert.Contains("type WidgetKind = 'basic' | 'fancy';", types);
		Assert.Contains("interface Paged<T>", types);
		Assert.DoesNotContain("export interface Widget", types);
	}

	[Fact]
	public void Generates_a_typed_data_interface_per_page()
	{
		Run();

		var types = ReadGeneratedFile("Svelte", "Home.d.ts");
		Assert.Contains("interface HomeData", types);
		Assert.Contains("title: string;", types);
		Assert.Contains("widgets: Widget[];", types);
		Assert.Contains("problem: { title: string; status: number; errors: Record<string, string[]> } | null;", types);
		Assert.Contains("antiforgeryToken: string;", types);
		Assert.DoesNotContain("import type", types);
	}

	[Fact]
	public void Nested_pages_share_the_ambient_declaration_file()
	{
		Run();

		var types = ReadGeneratedFile("Svelte", "Admin", "Users.d.ts");
		Assert.Contains("interface AdminUsersData", types);
		Assert.Contains("users: Paged<Widget>;", types);
		Assert.False(File.Exists(Path.Combine(_root, "Svelte", "Admin", "Users.types.d.ts")));
		Assert.Contains(
			"let { data }: { data: AdminUsersData } = $props();",
			ReadSvelteFile("Admin", "Users.svelte"));
	}

	[Fact]
	public void Scaffolds_a_runes_mode_component_when_missing()
	{
		Run();

		var component = ReadSvelteFile("Home.svelte");
		Assert.DoesNotContain("import type", component);
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
		var typesPath = Path.Combine(_root, ".svelte-net", "types", "Svelte", "Home.d.ts");
		File.WriteAllText(typesPath, "// stale");

		Run();

		Assert.DoesNotContain("stale", File.ReadAllText(typesPath));
	}

	[Fact]
	public void Scaffolds_runtime_helpers_and_vite_config()
	{
		Run();

		Assert.False(File.Exists(Path.Combine(_root, "Svelte", "mount.ts")));
		Assert.False(File.Exists(Path.Combine(_root, "Svelte", "render.ts")));
		var viteConfig = File.ReadAllText(Path.Combine(_root, "vite.config.ts"));
		Assert.Contains("import { sveltenet } from 'sveltenet/vite';", viteConfig);
		Assert.Contains("plugins: [sveltenet()]", viteConfig);
		Assert.Contains("\"sveltenet\":", File.ReadAllText(Path.Combine(_root, "package.json")));
	}

	[Fact]
	public void Non_default_source_roots_keep_project_wide_vite_discovery()
	{
		_options.PagesPath = "Islands";
		Run();

		var viteConfig = File.ReadAllText(Path.Combine(_root, "vite.config.ts"));
		Assert.Contains("plugins: [sveltenet()]", viteConfig);
		Assert.DoesNotContain("pagesPath", viteConfig);
		Assert.True(File.Exists(Path.Combine(_root, ".svelte-net", "types", "Islands", "Home.d.ts")));
	}

	[Fact]
	public void Component_models_get_types_and_a_scaffolded_component()
	{
		Run();

		Assert.Contains("interface CardViewModel", ReadGeneratedFile("models.d.ts"));

		var component = ReadSvelteFile("Components", "Card.svelte");
		Assert.DoesNotContain("import type", component);
		Assert.Contains("let { data }: { data: CardViewModel } = $props();", component);
	}

	[Fact]
	public void Remote_services_get_a_kit_style_typed_client()
	{
		Run();

		var remote = ReadSvelteFile("WidgetApi.remote.ts");
		Assert.Contains("import { command, form, query } from 'sveltenet/remote';", remote);
		Assert.DoesNotContain("import type", remote);
		Assert.Contains("export class WidgetApi", remote);
		Assert.Contains("static readonly Search = query<Widget[], [term: string, limit: number]>('WidgetApi/Search', (term, limit) => ({ term, limit }));", remote);
		Assert.Contains("static readonly Clear = command<void>('WidgetApi/Clear');", remote);
		Assert.Contains("static readonly Save = form<number, { widget: Widget }>('WidgetApi/Save');", remote);
	}

	[Fact]
	public void Remote_services_get_separate_client_classes()
	{
		SvelteScaffolder.Run(_options, [typeof(HomeModel)], [], [typeof(WidgetApi), typeof(CatalogApi)]);

		Assert.Contains("class WidgetApi", ReadSvelteFile("WidgetApi.remote.ts"));
		Assert.Contains("class CatalogApi", ReadSvelteFile("CatalogApi.remote.ts"));
	}

	[Fact]
	public void Feature_remote_clients_are_colocated_by_namespace()
	{
		SvelteScaffolder.Run(_options, [], [], [typeof(InventoryApi)]);

		var remote = ReadSvelteFile("Inventory", "InventoryApi.remote.ts");
		Assert.Contains("export class InventoryApi", remote);
		Assert.Contains("static readonly GetStock", remote);
	}

	[Fact]
	public void Removing_all_remote_services_removes_the_stale_client()
	{
		Run();
		Assert.Contains("static readonly Search", ReadSvelteFile("WidgetApi.remote.ts"));

		SvelteScaffolder.Run(_options, [], [], []);

		Assert.False(File.Exists(Path.Combine(_root, "Svelte", "WidgetApi.remote.ts")));
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

		var routes = ReadGeneratedFile("routes.d.ts");
		Assert.Contains("\"/\"", routes);
		Assert.Contains("\"/Admin/Users\"", routes);
		Assert.DoesNotContain("_Layout", routes);
	}

	[Fact]
	public void Removing_all_pages_replaces_stale_route_ids()
	{
		var pages = Path.Combine(_root, "Pages");
		Directory.CreateDirectory(pages);
		var page = Path.Combine(pages, "Index.cshtml");
		File.WriteAllText(page, "@page");
		Run();
		Assert.Contains("\"/\"", ReadGeneratedFile("routes.d.ts"));

		File.Delete(page);
		Run();

		Assert.Contains("type RouteId = never | (string & {});", ReadGeneratedFile("routes.d.ts"));
	}
}
