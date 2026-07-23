import type { Options as SvelteOptions } from '@sveltejs/vite-plugin-svelte';
import type { PluginOption } from 'vite';

export interface SvelteNetOptions {
	/** Directory containing the Svelte sources. Must match SvelteOptions.PagesPath on the .NET side. @default 'Svelte' */
	pagesPath?: string;
	/** Client build output. Must match SvelteOptions.ClientOutput. @default 'wwwroot/client' */
	clientOutDir?: string;
	/** SSR build output. Must match SvelteOptions.ServerOutput. @default 'svelte-ssr' */
	serverOutDir?: string;
	/** Options forwarded to @sveltejs/vite-plugin-svelte. */
	svelte?: SvelteOptions;
}

export declare function sveltenet(options?: SvelteNetOptions): PluginOption;
