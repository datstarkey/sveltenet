namespace SvelteNet.AspNetCore.Dev;

/// <summary>
/// Write-once file templates scaffolded in dev mode. Users own them after creation.
/// </summary>
internal static class SvelteTemplates
{
	public static string Page(string dataType) => $$"""
<script lang="ts">
	let { data }: { data: {{dataType}} } = $props();
</script>
""";

	public static string ComponentModelPage(string typeName) => $$"""
<script lang="ts">
	let { data }: { data: {{typeName}} } = $props();
</script>
""";

	public static string ViteConfig(SvelteOptions options)
	{
		var plugin = "sveltenet()";
		if (options.ClientOutput != "wwwroot/client" || options.ServerOutput != ".svelte-net/server")
		{
			plugin = $$"""
sveltenet({
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

	public static string TsConfig(SvelteOptions options) => $$"""
{
	"compilerOptions": {
		"allowJs": true,
		"checkJs": true,
		"esModuleInterop": true,
		"forceConsistentCasingInFileNames": true,
		"module": "esnext",
		"moduleResolution": "bundler",
		"noEmit": true,
		"resolveJsonModule": true,
		"skipLibCheck": true,
		"strict": true,
		"target": "es2022"
	},
	"include": [
		"{{options.PagesPath}}/**/*.js",
		"{{options.PagesPath}}/**/*.ts",
		"{{options.PagesPath}}/**/*.svelte",
		".svelte-net/**/*.d.ts",
		"vite.config.ts"
	]
}
""";

	public const string PackageJson = """
{
	"private": true,
	"type": "module",
	"scripts": {
		"dev": "vite",
		"build": "vite build",
		"check": "svelte-check --tsconfig ./tsconfig.json"
	},
	"devDependencies": {
		"svelte": "^5.46.0",
		"svelte-check": "^4.0.0",
		"sveltenet": "^0.1.0",
		"typescript": "^6.0.0",
		"vite": "^8.0.0"
	}
}
""";
}
