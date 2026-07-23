import { svelte } from '@sveltejs/vite-plugin-svelte';
import { globSync } from 'node:fs';

/**
 * All-in-one Vite setup for SvelteNet apps. Defines the client and SSR builds as
 * Vite environments with a builder hook, so a single `vite build` produces both —
 * configured to match what the .NET renderer expects: manifests on, stable entry
 * keys under the pages directory, entry exports preserved for the inline hydration
 * script, and a fully bundled SSR output (the .NET host has no node_modules).
 *
 * @param {import('./vite.d.ts').SvelteNetOptions} [options]
 * @returns {import('vite').PluginOption}
 */
export function sveltenet(options = {}) {
	const {
		pagesPath = 'Svelte',
		clientOutDir = 'wwwroot/client',
		serverOutDir = 'svelte-ssr',
		svelte: svelteOptions,
	} = options;

	/** @param {string} entry @param {string} outDir */
	const buildConfig = (entry, outDir) => ({
		manifest: true,
		target: 'es2022',
		outDir,
		emptyOutDir: true,
		rollupOptions: {
			// Entry exports must survive — the hydration script and the SSR engine import them.
			preserveEntrySignatures: 'allow-extension',
			input: [entry, ...globSync(`${pagesPath}/**/*.svelte`)],
		},
	});

	/** @type {import('vite').Plugin} */
	const configPlugin = {
		name: 'sveltenet',
		config() {
			return {
				environments: {
					client: {
						build: buildConfig(`${pagesPath}/mount.ts`, clientOutDir),
					},
					ssr: {
						build: buildConfig(`${pagesPath}/render.ts`, serverOutDir),
						resolve: {
							// Bundle everything into the SSR output — the .NET host has no node_modules.
							noExternal: true,
						},
					},
				},
				builder: {
					async buildApp(builder) {
						await builder.build(builder.environments.client);
						await builder.build(builder.environments.ssr);
					},
				},
			};
		},
	};

	return [svelte(svelteOptions), configPlugin];
}
