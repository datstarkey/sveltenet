# Dev mode & hot reload

In Development the renderer emits a different script — instead of hashed production assets, it imports straight from the Vite dev server:

```html
<script type="module">
    import "http://localhost:5173/@vite/client";        <!-- HMR runtime + websocket -->
    import { mountComponent } from "http://localhost:5173/Svelte/mount.ts";
    import App from "http://localhost:5173/Svelte/Index.svelte";
    mountComponent(App, { target: ..., hydrate: false, props: {...} });
</script>
```

ASP.NET serves the page and the props; Vite serves the components with transform-on-demand and pushes hot updates over its websocket. The daily workflow is two terminals:

```sh
dotnet watch run     # C# hot reload; scaffolder regenerates .types.ts on restart
npm run dev          # Svelte HMR — component edits appear in ~1s, no page reload
```

Caveats to know about:

- **SSR is skipped in dev** — the island mounts client-side (`mount`, not `hydrate`). Real SSR output only appears in a production run.
- **Component `$state` resets on hot update** — vite-plugin-svelte re-instantiates the component. Server-provided `data` is unaffected.
- **Changing a `[SvelteProp]`** flows into the generated TypeScript when the .NET process restarts (which `dotnet watch` does for you).
- Vite allows localhost origins by default, so the cross-origin module imports need no CORS config.
