import type { Options as SvelteOptions } from '@sveltejs/vite-plugin-svelte';
import type { PluginOption } from 'vite';

export interface SvelteNetOptions {
	/** Legacy single source root. Omit to discover project-wide Svelte files. */
	pagesPath?: string;
	/** Project-relative Svelte source globs. Ignored when pagesPath is set. @default ['**\/*.svelte'] */
	include?: string[];
	/** Globs excluded from source discovery. */
	exclude?: string[];
	/** Client build output. Must match SvelteOptions.ClientOutput. @default 'wwwroot/client' */
	clientOutDir?: string;
	/** SSR build output. Must match SvelteOptions.ServerOutput. @default '.svelte-net/server' */
	serverOutDir?: string;
	/** Enables Svelte's experimental await expressions (`compilerOptions.experimental.async`). @default false */
	experimentalAsync?: boolean;
	/** Options forwarded to @sveltejs/vite-plugin-svelte. */
	svelte?: SvelteOptions;
}

export declare function sveltenet(options?: SvelteNetOptions): PluginOption;
