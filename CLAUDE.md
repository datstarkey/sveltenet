# SvelteNet

Svelte 5 islands for ASP.NET (Razor Pages/MVC) with typed props, SvelteKit-style forms and remote functions, Jint-based SSR, and a Vite plugin. Monorepo: NuGet packages in `src/`, the `sveltenet` npm package in `packages/sveltenet`, sample in `samples/TodoApp`, docs in `docs/`.

## Commands

```sh
dotnet test SvelteNet.slnx                    # all .NET tests (integration tests boot samples/TodoApp)
cd packages/sveltenet && bun run test         # JS tests (node --test; runes modules are NOT node-testable)
bun install                                   # workspace install (bun.lock committed; npm/pnpm also work)
cd samples/TodoApp && bun run build           # ONE vite build produces client + SSR bundles
cd samples/TodoApp && dotnet run              # Production needs the vite build first; Development doesn't
```

## Docs are part of every change

`docs/*.md` is the user-facing documentation and **must stay in sync with the code**. When you change public API surface, generated file shapes, scaffolded templates, options, or protocols, update the matching doc in the same change:

- `docs/getting-started.md` — setup flow, scaffolded file list
- `docs/dev-mode.md` — dev/HMR behavior, `dotnet watch` + vite workflow
- `docs/forms.md` — SveltePage forms, `modelState`, antiforgery, `enhance()`
- `docs/mvc.md` — `[SvelteComponent]`, `@Html.Svelte`, duplicate-render rules
- `docs/remote-functions.md` — `[Query]`/`[Command]`/`[Form]`, client API, generator
- `docs/options.md` — `SvelteOptions` ↔ vite plugin option pairing, SSR engine
- `README.md` — overview + doc links only; details belong in docs/
- `packages/sveltenet/README.md` — npm-facing plugin/runtime docs

Code samples in docs must compile against the current API — treat a doc snippet like a test that isn't executed. If behavior in a doc no longer matches reality, fixing the doc is part of the task, not a follow-up.

## Architecture invariants

- **SvelteNet.Core stays host-agnostic** — no MVC/Razor/HTTP dependencies (Blazor hosting is planned). New transports go in `SvelteNet.AspNetCore` as minimal-API endpoints, not MVC filters.
- **One serialization contract**: `SvelteJson.Options` (camelCase props, dict keys, enums). The TS generator (`TypeGen/`) must always match it — a TS type that disagrees with the JSON is a bug.
- **Paths contract**: manifest keys are `{PagesPath}/{Component}.svelte`; `SvelteOptions` (C#) and `sveltenet()` vite plugin options must agree (`PagesPath`/`pagesPath`, `ClientOutput`/`clientOutDir`, `ServerOutput`/`serverOutDir`). The SSR bundle is deliberately NOT under wwwroot.
- **Remote dispatch is descriptor-based**: `SvelteNet.Generators` emits compiled dispatchers (module initializer → `SvelteRemoteDescriptors`); reflection in `SvelteRemoteDescriptors.FromReflection` is the fallback and must stay behaviorally identical to generated code. The scaffolder generates `Svelte/remote.ts` from the same descriptors.
- **Scaffolder rules**: `*.types.ts`, `types.ts`, `remote.ts`, `routes.d.ts` are regenerated every dev run ("do not edit" headers); `.svelte` components, `mount.ts`, `render.ts`, `vite.config.ts`, `package.json` are write-once and user-owned after creation.
- **SSR limits**: Jint has no `fetch`/Node APIs. Queries stay `loading` during SSR; await-expression components must set `ComponentOptions.Ssr = false`. Vite SSR builds bundle everything (`noExternal`) because production has no node_modules.
- **`preserveEntrySignatures` is load-bearing** in the vite plugin — without it Rollup/Rolldown treeshakes entry exports and hydration silently breaks.

## Conventions

- Tabs for indentation (C#, JS, Svelte). Svelte 5 runes only — no legacy syntax; follow the svelte:svelte-core-bestpractices skill for any .svelte work.
- New features need tests at the right layer: TypeGen/renderer → `SvelteNet.Core.Tests`; scaffolder/page/endpoints → `SvelteNet.AspNetCore.Tests` (integration via `TodoAppFactory` boots the sample in dev mode with scaffolding disabled); transport helpers → `packages/sveltenet/*.test.mjs`.
- The sample (`samples/TodoApp`) doubles as the integration-test host AND the living demo — new features should appear there.
- Generated `remote.ts` in the sample is committed; regenerate it by running the sample in Development after changing `TodoApi` or the generator output shape.
