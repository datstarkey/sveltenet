# Samples

Each sample demonstrates one slice of SvelteNet and is fully self-contained (own
`package.json`, `vite.config.ts`, and colocated component tree). The samples opt into
Jint explicitly with `AddSvelteNet().AddJintSSR()`.

| Sample | Demonstrates |
| --- | --- |
| [TodoApp](TodoApp/) | Razor Pages with typed `[SvelteProp]` props, SvelteKit-style form posts, and `enhance()` for no-reload submissions |
| [RemoteFunctions](RemoteFunctions/) | `[SvelteRemote]` query/command/form functions with the generated typed client, experimental async `await` in components, and fully awaited SSR via the in-process fetch bridge |
| [MvcHello](MvcHello/) | MVC controllers with `[SvelteComponent]` view models and `@Html.Svelte(Model)` |

Running any sample:

```sh
cd samples/<Sample>
dotnet run          # Development; dotnet build generates .svelte-net/types
bun run dev         # in a second terminal: vite + HMR

# production
bun run build       # one vite build produces client + SSR bundles
ASPNETCORE_ENVIRONMENT=Production dotnet run
```

`TodoApp` and `RemoteFunctions` double as hosts for the integration tests in `tests/SvelteNet.AspNetCore.Tests`.
