# Roadmap

Working list of where SvelteNet is heading. Items move to **Shipped** when they land — keep this file honest: if scope changes or something ships, update it in the same change.

## Next

- **Publish packages** — publish `SvelteNet.Core`, `SvelteNet.AspNetCore`, `SvelteNet.Jint`, and `SvelteNet.FluentValidation` to NuGet, plus `sveltenet` to npm. Packaging is complete (license metadata, Source Link, symbols, multi-targeted `net8.0;net10.0`, analyzer and build tool resolved from real project outputs, all four packed in CI); remaining work is versioning and the release flow.

## Later

- **Blazor host** — render islands from Blazor components. `SvelteNet.Core` stays host-agnostic specifically for this; new transports are minimal-API endpoints, not MVC filters.
- **Blazor SSR with enhanced navigation** — the real prize. Enhanced nav does NOT re-execute inline `<script>` tags on navigation, so the current inline-mount emission won't hydrate islands after a nav; needs an island bootstrapper (external module / custom element that discovers and mounts islands on DOM changes).
- **Scaffolder page-directory mapping** — currently assumes the `*.Pages.*` namespace convention; should fall back gracefully for custom layouts. Related: `SveltePage.SvelteComponent` derives the path from `ViewEnginePath` at runtime while the scaffolder derives it from the namespace, so the two can disagree.
- **Component-name safety** — a `SvelteComponents` constants generator + analyzers so string component references (`@await Html.SvelteAsync("Components/Card", ...)`) are checked at compile time.
- **Persistent SSR worker for Node.js/Bun** — those renderers spawn a process per render, which costs tens of milliseconds per island. A warm worker pool would make them competitive with Jint on latency without Jint's shared-module-state caveat.
- **Antiforgery for remote functions** — commands and forms are defended by the `X-SvelteNet` header plus an Origin check. An opt-in token requirement would add defence in depth for apps that want it.
- **Cancellation for queries** — `RemoteQuery` has no `AbortSignal`, so navigating away leaves requests in flight. Refresh races are already handled by generation counters.
- **No-JS form validation returns problem JSON** — a `[Form]` post from a browser without JavaScript renders the raw `application/problem+json` document instead of redirecting back with the errors. Fixing it properly needs the errors carried across the redirect (TempData or equivalent), which is a feature rather than a patch. Enhanced submits are unaffected.
- **Metadata generator holds Roslyn symbols in the incremental pipeline** — `SvelteMetadataGenerator` collects `INamedTypeSymbol`s, which roots the `Compilation` and defeats incremental caching, so a large solution re-runs full semantic analysis on every keystroke in the IDE. Needs projecting to equatable value models inside the transform.

## Shipped

- Svelte 5 / ASP.NET rewrite: typed `[SvelteProp]` props and generated TypeScript
- Opt-in SSR builder: client-only by default, pooled in-process Jint (`SvelteNet.Jint`), bundled Node.js/Bun CLI renderers, and custom renderer hooks
- One-command Vite build (client + SSR via the Environments API), dev mode with HMR
- SvelteKit-style forms with `enhance()`, including automatic antiforgery and double-submit protection
- `[SvelteComponent]` typed MVC rendering
- Remote functions (`[Query]`/`[Command]`/`[Form]`) with generated typed client and source-generated, descriptor-based dispatch *and* registration (no runtime reflection)
- Experimental async: awaited queries in components, SSR via the in-process fetch bridge, `hydratable` head stash with zero-refetch hydration, reactive-thenable refresh semantics
- Build-time TypeScript generation (`SvelteNet.Build` MSBuild target), incremental so repeat builds do no work
- SvelteKit-style `.svelte-net/types` declaration tree and private `.svelte-net/server` output
- Project-wide Svelte discovery with Razor/MVC/vertical-slice colocation and one generated remote class per C# service
- PR CI covering .NET tests/format/pack on Linux **and Windows**, JS Node + browser tests, sample typechecks/builds, and docs
- Samples split by concept (`TodoApp`, `RemoteFunctions`, `MvcHello`), all integration-tested
- VitePress docs site deployed to GitHub Pages; Dependabot across actions/bun/NuGet
- Validation errors as RFC 9457 problem details (`application/problem+json`, ASP.NET `errors` member) across remote functions, the SSR bridge, and enhanced page posts
- BYOV validation pipeline: `ISvelteRemoteValidator`s run between binding and invocation; DataAnnotations on parameters and complex argument types validate automatically
- `SvelteNet.FluentValidation`: registered `IValidator<T>`s (FluentValidation, still free/Apache 2.0 — worth sponsoring) run automatically via the BYOV pipeline
- **Asynchronous SSR contract** — `ISvelteSsrEngine.RenderAsync` and `ISvelteSsrFetchHandler.HandleAsync`. A server render can await remote queries; the previous synchronous contract blocked a request thread for the whole await chain. Jint, which cannot suspend a running script, renders on a worker capped by `MaxConcurrentRenders`
- **Descriptor-level authorization** — `[Authorize]`/`[AllowAnonymous]` on remote services and methods are enforced identically over HTTP and through the in-process SSR bridge, which bypasses routing entirely
- **Lossless 64-bit numerics** — `long`/`ulong`/`decimal` cross the wire as JSON strings and type as `string`, instead of silently rounding above 2^53
- **Collision-free generated type names** — namespace qualification when short names clash or would shadow a DOM global, with `[SvelteType]` to override
- **MIT licensed**, with license metadata on every package
