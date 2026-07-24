# Roadmap

Working list of where SvelteNet is heading. Items move to **Shipped** when they land — keep this file honest: if scope changes or something ships, update it in the same change.

## Next

- **Publish packages** — `SvelteNet.Core` + `SvelteNet.AspNetCore` + `SvelteNet.FluentValidation` to NuGet (bundling `SvelteNet.Generators` as an analyzer and `SvelteNet.Build.targets` in the package `build/` folder so consumers get build-time type generation automatically), and the `sveltenet` package to npm. Includes a versioning/release flow.
- **Test CI** — the docs deploy is the only workflow; PRs (including Dependabot's) should run `dotnet test` + the JS tests + sample vite builds.

## Later

- **Blazor host** — render islands from Blazor components. `SvelteNet.Core` stays host-agnostic specifically for this; new transports are minimal-API endpoints, not MVC filters.
- **Blazor SSR with enhanced navigation** — the real prize. Enhanced nav does NOT re-execute inline `<script>` tags on navigation, so the current inline-mount emission won't hydrate islands after a nav; needs an island bootstrapper (external module / custom element that discovers and mounts islands on DOM changes).
- **Scaffolder page-directory mapping** — currently assumes the `*.Pages.*` namespace convention; should fall back gracefully for custom layouts.
- **Component-name safety** — a `SvelteComponents` constants generator + analyzers so string component references (`@Html.Svelte("Components/Card", ...)`) are checked at compile time.

## Shipped

- Svelte 5 / ASP.NET 10 rewrite: typed `[SvelteProp]` props, TS generation, Jint SSR with engine pooling
- One-command Vite build (client + SSR via the Environments API), dev mode with HMR
- SvelteKit-style forms with `enhance()`
- `[SvelteComponent]` typed MVC rendering
- Remote functions (`[Query]`/`[Command]`/`[Form]`) with generated typed client and source-generated, descriptor-based dispatch *and* registration (no runtime reflection)
- Experimental async: awaited queries in components, SSR via the in-process fetch bridge, `hydratable` head stash with zero-refetch hydration, reactive-thenable refresh semantics
- Build-time TypeScript generation (`SvelteNet.Build` MSBuild target)
- Samples split by concept (`TodoApp`, `RemoteFunctions`, `MvcHello`), all integration-tested
- VitePress docs site deployed to GitHub Pages; Dependabot across actions/bun/NuGet
- Validation errors as RFC 9457 problem details (`application/problem+json`, ASP.NET `errors` member) across remote functions, the SSR bridge, and enhanced page posts
- BYOV validation pipeline: `ISvelteRemoteValidator`s run between binding and invocation; DataAnnotations on parameters and complex argument types validate automatically
- `SvelteNet.FluentValidation`: registered `IValidator<T>`s (FluentValidation, still free/Apache 2.0 — worth sponsoring) run automatically via the BYOV pipeline
