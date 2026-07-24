# Roadmap

Working list of where SvelteNet is heading. Items move to **Shipped** when they land — keep this file honest: if scope changes or something ships, update it in the same change.

## Next

- **Publish packages** — publish `SvelteNet.Core`, `SvelteNet.AspNetCore`, and `SvelteNet.FluentValidation` to NuGet, plus `sveltenet` to npm. Packaging already bundles the analyzer and build-time generator; remaining work is versioning and the release flow.

## Later

- **Blazor host** — render islands from Blazor components. `SvelteNet.Core` stays host-agnostic specifically for this; new transports are minimal-API endpoints, not MVC filters.
- **Blazor SSR with enhanced navigation** — the real prize. Enhanced nav does NOT re-execute inline `<script>` tags on navigation, so the current inline-mount emission won't hydrate islands after a nav; needs an island bootstrapper (external module / custom element that discovers and mounts islands on DOM changes).
- **Scaffolder page-directory mapping** — currently assumes the `*.Pages.*` namespace convention; should fall back gracefully for custom layouts.
- **Component-name safety** — a `SvelteComponents` constants generator + analyzers so string component references (`@Html.Svelte("Components/Card", ...)`) are checked at compile time.

## Shipped

- Svelte 5 / ASP.NET 10 rewrite: typed `[SvelteProp]` props and generated TypeScript
- Opt-in SSR builder: client-only by default, pooled in-process Jint, bundled Node.js/Bun CLI renderers, and custom renderer hooks
- One-command Vite build (client + SSR via the Environments API), dev mode with HMR
- SvelteKit-style forms with `enhance()`
- `[SvelteComponent]` typed MVC rendering
- Remote functions (`[Query]`/`[Command]`/`[Form]`) with generated typed client and source-generated, descriptor-based dispatch *and* registration (no runtime reflection)
- Experimental async: awaited queries in components, SSR via the in-process fetch bridge, `hydratable` head stash with zero-refetch hydration, reactive-thenable refresh semantics
- Build-time TypeScript generation (`SvelteNet.Build` MSBuild target)
- SvelteKit-style `.svelte-net/types` declaration tree and private `.svelte-net/server` output
- Project-wide Svelte discovery with Razor/MVC/vertical-slice colocation and one generated remote class per C# service
- PR CI covering .NET tests/format/pack, JS Node + browser tests, sample typechecks/builds, and docs
- Samples split by concept (`TodoApp`, `RemoteFunctions`, `MvcHello`), all integration-tested
- VitePress docs site deployed to GitHub Pages; Dependabot across actions/bun/NuGet
- Validation errors as RFC 9457 problem details (`application/problem+json`, ASP.NET `errors` member) across remote functions, the SSR bridge, and enhanced page posts
- BYOV validation pipeline: `ISvelteRemoteValidator`s run between binding and invocation; DataAnnotations on parameters and complex argument types validate automatically
- `SvelteNet.FluentValidation`: registered `IValidator<T>`s (FluentValidation, still free/Apache 2.0 — worth sponsoring) run automatically via the BYOV pipeline
