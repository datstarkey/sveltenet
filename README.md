# SvelteNet

Svelte 5 islands for ASP.NET Razor Pages and MVC — SvelteKit-style typed `data` props, forms, and remote functions, powered by your C# models.

- **Svelte 5 / runes** — components receive props via `$props()`, mounted with `mount`/`hydrate`; experimental await expressions supported (`sveltenet({ experimentalAsync: true })`).
- **SSR without Node in production** — the Vite SSR bundle runs in-process via [Jint](https://github.com/sebastienros/jint) (~0.5ms warm renders, pooled engines). Swap in your own `ISvelteSsrEngine` if you'd rather run a Node sidecar.
- **Typed everything, generated for you** — `[SvelteProp]` page models, `[SvelteComponent]` MVC view models, and `[SvelteRemote]` services all emit TypeScript; remote dispatch is compiled by a Roslyn source generator.
- **SvelteKit-style remote functions** — `[Query]`/`[Command]`/`[Form]` methods become `query`/`command`/`form` clients with caching, `refresh()`, `.updates()`, fields/issues state, and no-JS fallbacks.
- **One-plugin Vite setup** — `plugins: [sveltenet()]` builds client + SSR bundles in a single `vite build`; full HMR in development.

## Quickstart

```csharp
// Program.cs
using SvelteNet.AspNetCore;
builder.Services.AddRazorPages();
builder.Services.AddSvelteNet();
var app = builder.Build();
app.UseStaticFiles();
app.UseSvelteNet();          // dev: scaffolds + generates types; maps /_sveltenet/remote
app.MapRazorPages();
```

```csharp
public class IndexModel : SveltePage
{
    [SvelteProp] public List<Todo> Todos { get; set; } = [];
}
```

```cshtml
@model IndexModel
@section Head { @Model.SvelteHead() }
@Model.Svelte()
```

Run once in Development and the frontend is scaffolded — typed `.svelte` components, `vite.config.ts`, `package.json`. Then `npm install` + `npm run dev` alongside `dotnet watch run`.

## Documentation

- [Getting started](docs/getting-started.md) — setup, scaffolding, project layout
- [Dev mode & hot reload](docs/dev-mode.md) — the two-terminal workflow, HMR, caveats
- [Forms & enhance](docs/forms.md) — page forms, `modelState`, antiforgery, progressive enhancement
- [MVC views](docs/mvc.md) — `[SvelteComponent]` typed rendering, `@Html.Svelte`
- [Remote functions](docs/remote-functions.md) — `[Query]`/`[Command]`/`[Form]`, the typed client, source-generated dispatch
- [Options & SSR engine](docs/options.md) — configuration reference, swapping the SSR engine

## Repository layout

```
src/       SvelteNet.Core          — renderer, Jint SSR engine, TypeScript generation (NuGet)
           SvelteNet.AspNetCore    — SveltePage, @Html.Svelte, remote endpoints, scaffolder (NuGet)
           SvelteNet.Generators    — Roslyn source generator for remote dispatch
           SvelteNet.Build         — MSBuild target: dotnet build generates the TS types
           SvelteNet.FluentValidation — FluentValidation adapter for the validation pipeline
packages/  sveltenet               — Vite plugin + client/remote runtimes (npm)
samples/   TodoApp                 — Razor Pages: typed props, forms, enhance()
           RemoteFunctions         — query/command/form remote functions, async SSR
           MvcHello                — MVC controllers with [SvelteComponent]
tests/     SvelteNet.Core.Tests, SvelteNet.AspNetCore.Tests
docs/      the documentation above (also a VitePress site: bun run docs:dev)
```

## Development

The repo uses bun (bun.lock is committed), but every script calls tools directly, so npm/pnpm work too.

```sh
dotnet test SvelteNet.slnx              # .NET unit + integration tests
bun install                             # workspace install (packages/ + samples/ + docs/)
cd packages/sveltenet && bun run test   # JS tests
cd samples/TodoApp && dotnet run        # any sample: see samples/README.md
bun run docs:dev                        # documentation site
```

## Status

Prototype — not yet published to NuGet or npm. See [ROADMAP.md](ROADMAP.md) for what's next (publishing, test CI, Blazor/Blazor-SSR hosting) and what has shipped.
