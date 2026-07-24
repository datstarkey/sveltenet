# sveltenet

Vite plugin and runtime helpers for [SvelteNet](https://github.com/datstarkey/sveltenet) — Svelte 5 islands for ASP.NET Razor Pages and MVC.

This package is the JS half of SvelteNet; the .NET half is the
`SvelteNet.AspNetCore` NuGet package, which renders pages, passes typed props, and
generates declarations/scaffolds missing files during `dotnet build`.

## Usage

```ts
// vite.config.ts
import { sveltenet } from 'sveltenet/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [sveltenet()],
});
```

The plugin configures everything the .NET renderer expects: client build to `wwwroot/client`, SSR build to `.svelte-net/server`, manifests, stable entry keys, preserved entry exports, and a fully bundled SSR output (no node_modules needed at runtime).

It uses Vite's Environments API with a builder hook (the same mechanism SvelteKit uses), so one command builds both bundles:

```sh
vite build           # client AND SSR bundles in one run
vite                 # dev server with HMR
```

### Options

```ts
sveltenet({
	include: ['**/*.svelte'],       // default: project-wide discovery
	exclude: ['Legacy/**'],         // added to the safe framework/build exclusions
	clientOutDir: 'wwwroot/client', // must match SvelteOptions.ClientOutput
	serverOutDir: '.svelte-net/server', // must match SvelteOptions.ServerOutput
	svelte: { /* forwarded to @sveltejs/vite-plugin-svelte */ },
})
```

`pagesPath` remains available as a legacy single-root filter. New projects can omit it:
components may live under `Svelte/`, beside Razor/MVC files, or inside vertical feature
slices.

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

- `sveltenet/server` — `renderComponent(component, props)`, executed only when the
  .NET application opts into Jint, Node.js, Bun, or a custom SSR engine.

The Vite plugin owns stable named entries for both runtimes; applications do not need
`mount.ts` or `render.ts` shims. Package source lives in `src/`, with its Node/browser
tests isolated under `test/`.

## Requirements

Svelte ^5.46, Vite ^8.
