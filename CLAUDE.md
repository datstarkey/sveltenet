# SvelteNet

Svelte 5 islands for ASP.NET (Razor Pages/MVC) with typed props, SvelteKit-style forms and remote functions, Jint-based SSR, and a Vite plugin. Monorepo: NuGet packages in `src/`, the `sveltenet` npm package in `packages/sveltenet`, samples in `samples/` (one concept each — see `samples/README.md`), docs in `docs/`.

## Commands

```sh
dotnet test SvelteNet.slnx                    # all .NET tests (integration tests boot the samples)
cd packages/sveltenet && bun run test         # JS tests (node --test; runes modules are NOT node-testable)
bun install                                   # workspace install (bun.lock committed; npm/pnpm also work)
cd samples/<Sample> && bun run build          # ONE vite build produces client + SSR bundles
cd samples/<Sample> && dotnet run             # launchSettings pin Development; production: bun run build,
                                              #   then ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile
bun run docs:build                            # VitePress docs site (config in docs/.vitepress/)
```

## Docs are part of every change

`docs/*.md` is the user-facing documentation — it is also built into a VitePress site (`docs/.vitepress/config.mts`; new pages must be added to its sidebar) and **must stay in sync with the code**. When you change public API surface, generated file shapes, scaffolded templates, options, or protocols, update the matching doc in the same change:

- `docs/getting-started.md` — setup flow, scaffolded file list
- `docs/dev-mode.md` — dev/HMR behavior, `dotnet watch` + vite workflow
- `docs/forms.md` — SveltePage forms, `data.problem` validation errors, antiforgery, `enhance()`
- `docs/mvc.md` — `[SvelteComponent]`, `@Html.Svelte`, duplicate-render rules
- `docs/remote-functions.md` — `[Query]`/`[Command]`/`[Form]`, client API, generator, SSR/hydration
- `docs/options.md` — `SvelteOptions` ↔ vite plugin option pairing, discovery scope, SSR engine
- `README.md` — overview + doc links only; details belong in docs/
- `packages/sveltenet/README.md` — npm-facing plugin/runtime docs
- `ROADMAP.md` — move items to Shipped when they land; add/adjust items when plans change. Completing a roadmap item without updating this file is an incomplete change.

Code samples in docs must compile against the current API — treat a doc snippet like a test that isn't executed. If behavior in a doc no longer matches reality, fixing the doc is part of the task, not a follow-up.

## Architecture invariants

- **SvelteNet.Core stays host-agnostic** — no MVC/Razor/HTTP dependencies (Blazor hosting is planned). New transports go in `SvelteNet.AspNetCore` as minimal-API endpoints, not MVC filters.
- **One serialization contract**: `SvelteJson.Options` (camelCase props, dict keys, enums). The TS generator (`TypeGen/`) must always match it — a TS type that disagrees with the JSON is a bug.
- **Validation errors are RFC 9457 problem details** (400, `application/problem+json`, ASP.NET `errors` member) everywhere: remote endpoints via `Results.ValidationProblem`, the SSR fetch bridge, and enhanced SveltePage posts (whose `data` extension member carries fresh props; SSR props expose the same shape as `data.problem`). `SvelteValidationException` is the throwing API. Never invent a bespoke error shape.
- **Paths contract**: manifest keys are `{PagesPath}/{Component}.svelte`; `SvelteOptions` (C#) and `sveltenet()` vite plugin options must agree (`PagesPath`/`pagesPath`, `ClientOutput`/`clientOutDir`, `ServerOutput`/`serverOutDir`). The SSR bundle is deliberately NOT under wwwroot.
- **Remote dispatch AND registration are descriptor-based**: `SvelteNet.Generators` emits compiled dispatchers (module initializer → `SvelteRemoteDescriptors`); `AddSvelteNet` consumes registered descriptors scoped to the app's assemblies — no runtime reflection scan. Reflection (`FromReflection` + the `TypeDiscovery` fallback) only covers apps without the analyzer and must stay behaviorally identical to generated code. The scaffolder generates `Svelte/remote.ts` from the same descriptors.
- **Discovery is assembly-scoped**: multiple SvelteNet apps share the test process, so `[SvelteRemote]`/`[SvelteComponent]`/`SveltePage` discovery must respect `SvelteOptions.ApplicationAssemblies` (defaults to the `AddSvelteNet` caller). An unscoped scan leaks one sample's services into another's container.
- **Type generation is build-time**: `SvelteNet.Build.targets` (imported by each sample, shipped in the NuGet's build/ later) runs `SvelteNet.Build` after every `dotnet build` — it loads the built app assembly in an isolated ALC and invokes the scaffolder via reflection (deliberately no SvelteNet project references, so the app's copy is the only copy). Startup scaffolding (`EnableScaffolding = true`) is only a fallback for apps without the targets.
- **Scaffolder rules**: `*.types.ts`, `types.ts`, `remote.ts`, `routes.d.ts` are regenerated every build ("do not edit" headers); `.svelte` components, `mount.ts`, `render.ts`, `vite.config.ts`, `package.json` are write-once and user-owned after creation. Each sample commits `wwwroot/.gitkeep` — Development crashes without the directory.
- **SSR queries work**: awaited queries resolve during SSR through the in-process fetch bridge (`ISvelteSsrFetchHandler`) and stash into the head via `hydratable`; `RemoteQuery` instances are per-render on the server (pooled engines keep module state alive). `then`/`catch`/`finally` on queries are property getters — the reactive read must happen during the synchronous `.then` property access. Jint has no Node APIs beyond the served shims (`node:async_hooks`); Vite SSR builds bundle everything (`noExternal`) because production has no node_modules.
- **`preserveEntrySignatures` is load-bearing** in the vite plugin — without it Rollup/Rolldown treeshakes entry exports and hydration silently breaks.

## Conventions

- Tabs for indentation (C#, JS, Svelte). Svelte 5 runes only — no legacy syntax; follow the svelte:svelte-core-bestpractices skill for any .svelte work.
- New features need tests at the right layer: TypeGen/renderer → `SvelteNet.Core.Tests`; scaffolder/page/endpoints → `SvelteNet.AspNetCore.Tests` (integration factories boot the samples in dev mode with scaffolding disabled: `TodoAppFactory` for forms/enhance, `RemoteFunctionsFactory` for remote functions, `MvcHelloFactory` for MVC); transport helpers → `packages/sveltenet/*.test.mjs`.
- The samples double as integration-test hosts AND living demos — new features should appear in the sample that owns the concept (`TodoApp` forms/props, `RemoteFunctions` remote+async, `MvcHello` MVC).
- Generated `remote.ts` in `samples/RemoteFunctions` is committed; `dotnet build` regenerates it after changing `TodoApi` or the generator output shape.
