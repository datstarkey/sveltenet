# sveltenet

Vite plugin and runtime helpers for [SvelteNet](https://github.com/datstarkey/sveltenet) — Svelte 5 islands for ASP.NET Razor Pages and MVC.

This package is the JS half of SvelteNet; the .NET half is the `SvelteNet.AspNetCore` NuGet package, which renders pages, passes typed props, and (in dev) scaffolds your Svelte files.

## Usage

```ts
// vite.config.ts
import { sveltenet } from 'sveltenet/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [sveltenet()],
});
```

The plugin configures everything the .NET renderer expects: client build to `wwwroot/client`, SSR build to `svelte-ssr`, manifests, stable entry keys, preserved entry exports, and a fully bundled SSR output (no node_modules needed at runtime).

It uses Vite's Environments API with a builder hook (the same mechanism SvelteKit uses), so one command builds both bundles:

```sh
vite build           # client AND SSR bundles in one run
vite                 # dev server with HMR
```

### Options

```ts
sveltenet({
	pagesPath: 'Svelte',            // must match SvelteOptions.PagesPath
	clientOutDir: 'wwwroot/client', // must match SvelteOptions.ClientOutput
	serverOutDir: 'svelte-ssr',     // must match SvelteOptions.ServerOutput
	svelte: { /* forwarded to @sveltejs/vite-plugin-svelte */ },
})
```

### Runtime entry points

- `sveltenet/client` — `mountComponent(component, { target, props, hydrate })`, called by the inline script the .NET renderer emits, and `enhance(options)` for progressive form enhancement:

```svelte
<script lang="ts">
    import { enhance } from 'sveltenet/client';

    let { data: serverData } = $props();
    let data = $derived(serverData);
</script>

<form method="post" {@attach enhance({ onUpdate: (d) => (data = d) })}>
```

Enhanced submits go over `fetch` with an `X-SvelteNet` header; the .NET side responds with fresh JSON data (following post/redirect/get automatically) and `onUpdate` applies it to the island — no page reload. Without JS the form posts normally.

- `sveltenet/server` — `renderComponent(component, props)`, executed inside the .NET host's SSR engine.

Your app's `Svelte/mount.ts` and `Svelte/render.ts` are one-line re-exports of these (scaffolded for you) — they exist only so the Vite manifest has stable in-project keys.

## Requirements

Svelte ^5.46, Vite ^8.
