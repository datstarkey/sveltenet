namespace SvelteNet.AspNetCore.Dev;

/// <summary>
/// Write-once file templates scaffolded in dev mode. Users own them after creation.
/// The mount/render entries are thin re-exports of the `sveltenet` npm package —
/// they exist in the app only because the Vite manifest needs stable in-project keys.
/// </summary>
internal static class SvelteTemplates
{
	public const string MountTs = """
export { mountComponent } from 'sveltenet/client';
""";

	public const string RenderTs = """
export { renderComponent } from 'sveltenet/server';
""";

	public static string Page(string name) => $$"""
<script lang="ts">
	import type { {{name}}Data } from './{{name}}.types';

	let { data }: { data: {{name}}Data } = $props();
</script>
""";

	public static string ComponentModelPage(string typeName, string typesImport) => $$"""
<script lang="ts">
	import type { {{typeName}} } from '{{typesImport}}';

	let { data }: { data: {{typeName}} } = $props();
</script>
""";

	public static string ViteConfig(SvelteOptions options)
	{
		var plugin = "sveltenet()";
		if (options.PagesPath != "Svelte" || options.ClientOutput != "wwwroot/client" || options.ServerOutput != "svelte-ssr")
		{
			plugin = $$"""
sveltenet({
			pagesPath: '{{options.PagesPath}}',
			clientOutDir: '{{options.ClientOutput}}',
			serverOutDir: '{{options.ServerOutput}}',
		})
""";
		}

		return $$"""
import { sveltenet } from 'sveltenet/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [{{plugin}}],
});
""";
	}

	public const string PackageJson = """
{
	"private": true,
	"type": "module",
	"scripts": {
		"dev": "vite",
		"build": "vite build"
	},
	"devDependencies": {
		"svelte": "^5.46.0",
		"sveltenet": "^0.1.0",
		"typescript": "^6.0.0",
		"vite": "^8.0.0"
	}
}
""";
}
