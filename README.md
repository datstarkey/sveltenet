# SvelteNet

Svelte 5 islands for ASP.NET Razor Pages and MVC — SvelteKit-style typed `data` props, powered by your C# models.

- **Svelte 5 / runes** — components receive props via `$props()`, mounted with `mount`/`hydrate`.
- **SSR without Node in production** — the Vite SSR bundle runs in-process via [Jint](https://github.com/sebastienros/jint). Warm renders are sub-millisecond (engines are pooled and cache their modules). Swap in your own `ISvelteSsrEngine` if you'd rather run a Node sidecar.
- **Typed props, generated for you** — every `[SvelteProp]` on a page model is emitted as TypeScript, so the Svelte side is fully typed against your C# models. Model-state errors and the antiforgery token ride along automatically.
- **One-plugin Vite setup** — the `sveltenet` npm package configures the whole client/SSR build; full HMR in development.

## Repository layout

```
src/       SvelteNet.Core          — renderer, Jint SSR engine, TypeScript generation (NuGet)
           SvelteNet.AspNetCore    — SveltePage, @Html.Svelte, DI, dev scaffolder (NuGet)
packages/  sveltenet               — Vite plugin + client/server runtime helpers (npm)
samples/   TodoApp                 — Razor Pages + MVC sample (forms, validation, nested pages)
tests/     SvelteNet.Core.Tests, SvelteNet.AspNetCore.Tests
```

## How it works

```
┌─ ASP.NET (net10.0) ─────────────┐      ┌─ Vite (sveltenet/vite plugin) ──┐
│ SveltePage / @Html.Svelte(...)  │      │ Svelte/**/*.svelte              │
│   [SvelteProp] props → JSON     │      │   client build → wwwroot/client │
│   SvelteRenderer                │      │   ssr build    → svelte-ssr     │
│     ├─ SSR: Jint runs svelte-ssr│◄─────┤   (manifest.json both)          │
│     └─ CSR: <script type=module>│      └─────────────────────────────────┘
│        hydrate(App, { data })   │
└─────────────────────────────────┘
```

Each Razor Page (or MVC view) renders a `<div>` with the server-rendered component inside, plus an inline module script that imports the built component and hydrates it with the same JSON props.

## Getting started

```csharp
// Program.cs
using SvelteNet.AspNetCore;

builder.Services.AddRazorPages();
builder.Services.AddSvelteNet();          // options: o => o.PagesPath = "Svelte", ...

var app = builder.Build();
app.UseStaticFiles();
app.UseSvelteNet();                       // dev only: generates types + scaffolds files
app.MapRazorPages();
```

```csharp
// Pages/Index.cshtml.cs
using SvelteNet;
using SvelteNet.AspNetCore;

public class IndexModel : SveltePage
{
    [SvelteProp] public string Title { get; set; } = "Hello";
    [SvelteProp] public List<Todo> Todos { get; set; } = [];
}
```

```cshtml
@* Pages/Index.cshtml *@
@model IndexModel
@section Head { @Model.SvelteHead() }
@Model.Svelte()
```

Run the app once in Development and SvelteNet scaffolds the frontend:

```
Svelte/
  types.ts            ← generated: TS interfaces for all [SvelteProp] model types
  routes.d.ts         ← generated: RouteId union of your page routes
  Index.types.ts      ← generated: interface IndexData { title: string; todos: Todo[]; ... }
  Index.svelte        ← scaffolded once, then yours
  mount.ts            ← one-line re-export of sveltenet/client (stable manifest key)
  render.ts           ← one-line re-export of sveltenet/server
vite.config.ts        ← scaffolded once: plugins: [sveltenet()]
package.json          ← scaffolded once
```

```svelte
<script lang="ts">
    import type { IndexData } from './Index.types';

    let { data }: { data: IndexData } = $props();
</script>
```

Then `npm install` (or bun/pnpm) and:

- **Development**: `dotnet watch run` + `npm run dev` in two terminals (see below).
- **Production**: `npm run build` (client + SSR bundles), then deploy. `wwwroot/client` is served statically; `svelte-ssr` is executed by Jint and is deliberately *not* under wwwroot.

## Dev mode & hot reload

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

### Forms & enhance (actions)

The `data` prop always includes `modelState` (`Record<string, string[]>` of validation errors) and `antiforgeryToken`. Razor Pages named handlers work as normal form actions — `action="?handler=toggle"` maps to `OnPostToggle(int id)`.

Without JavaScript, forms are plain POSTs with full page reloads. Attach `enhance` for SvelteKit-style progressive enhancement — the submit goes over `fetch` and the island updates in place:

```svelte
<script lang="ts">
    import { enhance } from 'sveltenet/client';
    import type { IndexData } from './Index.types';

    let { data: serverData }: { data: IndexData } = $props();
    // Writable $derived: tracks the server prop, assignable from enhanced responses.
    let data = $derived(serverData);

    const applyUpdate = enhance({ onUpdate: (d: IndexData) => (data = d) });
</script>

<form method="post" {@attach applyUpdate}>
    <input type="hidden" name="__RequestVerificationToken" value={data.antiforgeryToken} />
    <input name="NewLabel" />
    <button>Add</button>
</form>
```

How it works: `enhance` submits with an `X-SvelteNet` header. `SveltePage` answers enhanced requests with JSON instead of HTML:

- Handler returns `Page()` (e.g. validation failed) → the fresh props bag, so errors arrive in `data.modelState`.
- Handler redirects (post/redirect/get) → `{ "redirect": url }`; the client follows it with a second enhanced fetch and applies the fresh data (then resets the form; pass `reset: false` to keep values).
- Anything else (JSON, files, status codes) passes through untouched.
- A plain `GET` with the `X-SvelteNet` header returns the page's `data` as JSON — useful for polling or manual refreshes.

Enhanced forms degrade gracefully: no JS (or an `onError` you handle) means the normal POST flow. Override `CreateEnhancedResult` on your page model to customize the protocol. See `samples/TodoApp` for the full add/toggle/validation wiring.

### MVC / arbitrary views

Put `[SvelteComponent]` on the view model and the component is resolved from the type — no strings in views, the model's TypeScript interface lands in `types.ts`, and the component file is scaffolded on the next dev run:

```csharp
[SvelteComponent]                       // → "Components/Cart" (type name minus ViewModel/Model/Dto)
public record CartViewModel(List<Item> Items);

[SvelteComponent("Widgets/Special")]    // or name the path explicitly
public record SpecialViewModel(...);
```

```cshtml
@model CartViewModel
@section Head { @Html.SvelteHead(Model) }
@Html.Svelte(Model)
```

```svelte
<script lang="ts">
    import type { CartViewModel } from '../types';

    let { data }: { data: CartViewModel } = $props();
</script>
```

The string-based form also works for one-offs — `@Html.Svelte("Components/Cart", Model.Cart)` — where the model is passed as the component's `data` prop and you write the interface yourself.

Renders are cached per request so a `Svelte`/`SvelteHead` pair renders once. To render the same component multiple times on a page, give each instance a distinct `elementId` (it keys the cache and the container div id) — without one, the second render throws rather than silently reusing the first instance's output:

```cshtml
@Html.Svelte("Components/Card", Model.ProductA, elementId: "card-a")
@Html.Svelte("Components/Card", Model.ProductB, elementId: "card-b")
```

## Options

`AddSvelteNet(o => ...)` on the .NET side and `sveltenet({ ... })` in vite.config.ts must agree on the paths (defaults already do):

| .NET option | Vite plugin option | Default | |
|---|---|---|---|
| `PagesPath` | `pagesPath` | `Svelte` | Svelte source directory and manifest key prefix |
| `ClientOutput` | `clientOutDir` | `wwwroot/client` | Vite client build outDir |
| `ServerOutput` | `serverOutDir` | `svelte-ssr` | Vite SSR build outDir (never public) |
| `ClientPublicPath` | — | `/client` | URL prefix for built assets |
| `DevServerUrl` | — | `http://localhost:5173` | Vite dev server |
| `EnableSsr` / `EnableCsr` | — | `true` | Per-component override via `ComponentOptions` |
| `IsDev` | — | auto | From `IWebHostEnvironment.IsDevelopment()` |
| `EnableScaffolding` | — | follows `IsDev` | Type generation + scaffolding on startup |

## SSR engine

`JintSsrEngine` is the default: pure .NET, no Node at runtime, module-caching engine pool (~0.5ms warm renders). If your components need Node APIs or you want V8 throughput, implement `ISvelteSsrEngine` and register it before `AddSvelteNet`:

```csharp
builder.Services.AddSingleton<ISvelteSsrEngine, MyNodeSidecarEngine>();
```

## Development

The repo uses bun (bun.lock is committed), but every script calls tools directly, so npm/pnpm work too.

```sh
dotnet test SvelteNet.slnx        # .NET unit + integration tests (integration tests
                                  # boot the TodoApp sample via WebApplicationFactory)
bun install                       # workspace install (packages/ + samples/)
cd packages/sveltenet && bun run test   # JS tests for the enhance() helper
cd samples/TodoApp
bun run build                     # client + SSR bundles
dotnet run                        # then visit /, /Admin/Stats, /Hello
```

## Status

Prototype — not yet published to NuGet or npm. Known gaps: the scaffolder's page-directory mapping assumes the `*.Pages.*` namespace convention.
