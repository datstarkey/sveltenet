# Options

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
| `ApplicationAssemblies` | — | calling assembly | Discovery scope for `[SvelteRemote]`, `[SvelteComponent]`, and `SveltePage` types |

## Discovery scope

`AddSvelteNet()` discovers remote services from the **assembly that calls it** — generated descriptors register themselves via module initializers, so nothing is scanned at runtime. If your `[SvelteRemote]` services or page models live in other projects, pass their assemblies:

```csharp
builder.Services.AddSvelteNet(configure: null, typeof(MyApi).Assembly, typeof(Program).Assembly);
```

## SSR engine

`JintSsrEngine` is the default: pure .NET, no Node at runtime, module-caching engine pool (~0.5ms warm renders). If your components need Node APIs or you want V8 throughput, implement `ISvelteSsrEngine` and register it before `AddSvelteNet`:

```csharp
builder.Services.AddSingleton<ISvelteSsrEngine, MyNodeSidecarEngine>();
```
