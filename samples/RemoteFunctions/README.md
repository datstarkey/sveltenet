# RemoteFunctions

SvelteKit-style remote functions as vertical slices. Each C# remote class gets a
colocated generated TypeScript class:

- `Features/Todos/TodoApi.cs` → `TodoApi.remote.ts` → `TodoApi.GetTodos()`
- `Features/Weather/WeatherApi.cs` → `WeatherApi.remote.ts` → `WeatherApi.GetForecasts()`

Each feature owns its service, models, island, and typed client with no hand-written fetch code.
The sample explicitly selects the in-process backend with
`AddSvelteNet(...).AddJintSSR()`.

- **`/`** — `{#each await TodoApi.GetTodos() ...}` with experimental async: queries are awaited during SSR through the in-process fetch bridge, stashed into the page head via `hydratable`, and hydrated with zero refetches. Commands refresh queries with `.updates()`; the `[Form]` handler is spread straight onto a `<form>`.
- **`/Stats`** — the non-async pattern: SSR renders page props, a `query` takes over client-side with `current`/`loading`/`refresh()`.
- **`/Weather`** — a second independent feature and generated remote class.
