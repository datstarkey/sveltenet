# Options

The .NET source root controls page/component conventions and scaffolding. Vite discovers
all project-relative `*.svelte` files by default, so colocated components need no matching
Vite root configuration:

| .NET option | Vite plugin option | Default | |
|---|---|---|---|
| `PagesPath` | `pagesPath` (optional legacy filter) | `Svelte` | .NET convention/scaffolding root; Vite scans project-wide when `pagesPath` is omitted |
| `ClientOutput` | `clientOutDir` | `wwwroot/client` | Vite client build outDir |
| `ServerOutput` | `serverOutDir` | `.svelte-net/server` | Vite SSR build outDir (never public) |
| `ClientPublicPath` | — | `/client` | URL prefix for built assets |
| `DevServerUrl` | — | `http://localhost:5173` | Vite dev server |
| `EnableSsr` | — | `true` | Global switch for an explicitly registered SSR renderer; registering no renderer always means no SSR |
| `EnableCsr` | — | `true` | Global client mount/hydration switch |
| `IsDev` | — | auto | From `IWebHostEnvironment.IsDevelopment()` |
| `EnableScaffolding` | — | `false` | Startup scaffolding fallback; normally `dotnet build` generates types via `SvelteNet.Build.targets` |
| `EnableReflectionFallback` | — | `false` | Opt-in compatibility path for apps built without the analyzer |
| `ApplicationAssemblies` | — | calling assembly | Discovery scope for `[SvelteRemote]`, `[SvelteComponent]`, and `SveltePage` types |

The build target accepts matching MSBuild properties:
`SvelteNetPagesPath`, `SvelteNetClientOutput`, `SvelteNetServerOutput`, and
`SvelteNetApplicationAssemblies`. The `SvelteNet.AspNetCore` NuGet package bundles the
analyzer, build tool, and target, so ordinary consumers do not wire these pieces manually.

The Vite plugin accepts `include` (default `["**/*.svelte"]`) and `exclude`. Its default
exclusions are `node_modules`, `.svelte-net`, `bin`, `obj`, `wwwroot`, and `.git`.
This supports `Svelte/`, Razor/MVC colocation, and vertical slices such as
`Features/Todos/` without changing `vite.config.ts`.

## Discovery scope

`AddSvelteNet()` consumes generated descriptors from the **assembly that calls it** — page props, component mappings, remote registration, binding, validation metadata, dispatch, and TypeScript declarations are source-generated, so the normal path does not scan types or inspect members at runtime. If your SvelteNet types live in other projects, pass their assemblies:

```csharp
builder.Services.AddSvelteNet(configure: null, typeof(MyApi).Assembly, typeof(Program).Assembly);
```

For a legacy app that deliberately omits the analyzer, set
`EnableReflectionFallback = true`. Generated and fallback dispatchers share the same
binding and validation pipeline, but the fallback is not the default.

## SSR renderer options

`AddSvelteNet()` is client-only. Renderer-specific settings live with the renderer
instead of expanding `SvelteOptions`:

| Registration | Options | Defaults |
|---|---|---|
| `AddJintSSR(...)` | `Timeout`, `MaxPooledEngines` | 10 seconds, processor count |
| `AddNodeSSR(...)` | `ExecutablePath`, `Timeout`, `BaseUrl`, `ForwardHeaders` | `node`, 10 seconds, server address, auth + cookie |
| `AddBunJsSSR(...)` | `ExecutablePath`, `Timeout`, `BaseUrl`, `ForwardHeaders` | `bun`, 10 seconds, server address, auth + cookie |
| `AddCustomRenderer(...)` | Owned by the custom implementation | — |

```csharp
builder.Services
    .AddSvelteNet(options =>
    {
        options.EnableCsr = true;
        options.EnableSsr = true;
        options.ServerOutput = ".svelte-net/server";
    })
    .AddJintSSR(jint =>
    {
        jint.Timeout = TimeSpan.FromSeconds(5);
        jint.MaxPooledEngines = 4;
    });
```

The Node.js and Bun paths/timeouts are configured on `NodeSsrOptions` and
`BunJsSsrOptions` through their matching builder methods. See
[SSR renderers](ssr.md) for complete selection, CLI, request-forwarding, deployment,
and custom-engine behavior.
