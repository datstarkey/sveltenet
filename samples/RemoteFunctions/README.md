# RemoteFunctions

SvelteKit-style remote functions backed by a C# `[SvelteRemote]` service. `Svelte/remote.ts` is generated from `Services/TodoApi.cs` — typed `query`/`command`/`form` clients with no hand-written fetch code.

- **`/`** — `{#each await getTodos() ...}` with experimental async: queries are awaited during SSR through the in-process fetch bridge, stashed into the page head via `hydratable`, and hydrated with zero refetches. Commands refresh queries with `.updates()`; the `[Form]` handler is spread straight onto a `<form>`.
- **`/Stats`** — the non-async pattern: SSR renders page props, a `query` takes over client-side with `current`/`loading`/`refresh()`.
