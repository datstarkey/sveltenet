# SSR renderers

SSR is opt-in. `AddSvelteNet()` alone registers client rendering, generated types,
remote functions, and ASP.NET integration, but it does not register an
`ISvelteSsrEngine`. Production islands therefore mount with `hydrate: false` and
SvelteNet never reads or executes the server bundle.

Choose one renderer by chaining it from the builder:

```csharp
// In-process JavaScript; no external runtime is required.
builder.Services
    .AddSvelteNet()
    .AddJintSSR();

// V8 through an installed Node.js CLI.
builder.Services
    .AddSvelteNet()
    .AddNodeSSR();

// JavaScriptCore through an installed Bun CLI.
builder.Services
    .AddSvelteNet()
    .AddBunJsSSR();
```

Calling another renderer method on the same builder replaces the previous selection.
Per-island `ComponentOptions.Ssr = false` and the shared `SvelteOptions.EnableSsr =
false` can still disable an already configured renderer. Setting `Ssr = true` cannot
enable SSR when no engine was registered.

SSR is always skipped in Development because Vite serves source modules directly.
Build and run with a non-Development environment to inspect server-rendered output.

## Jint

Jint runs inside the ASP.NET process and needs no Node.js installation. Engines cache
their imported module graph and are pooled across renders:

```csharp
builder.Services.AddSvelteNet().AddJintSSR(options =>
{
    options.Timeout = TimeSpan.FromSeconds(5);
    options.MaxPooledEngines = Environment.ProcessorCount;
});
```

Awaited remote queries use `ISvelteSsrFetchHandler` to dispatch directly to the
generated C# remote descriptor. There is no loopback HTTP request.

## Node.js and Bun

The Node.js and Bun implementations are included in `SvelteNet.AspNetCore`; no extra
NuGet integration package is required. They launch the configured CLI for server
renders, import the built ESM component and `sveltenet/server` entry, and return the
same `SsrResult` contract as Jint.

```csharp
builder.Services.AddSvelteNet().AddNodeSSR(options =>
{
    options.ExecutablePath = "/opt/node/bin/node"; // default: "node"
    options.Timeout = TimeSpan.FromSeconds(8);
    options.BaseUrl = new Uri("http://127.0.0.1:5000"); // optional trusted override
    options.ForwardHeaders.Remove("Authorization");
});

builder.Services.AddSvelteNet().AddBunJsSSR(options =>
{
    options.ExecutablePath = "/opt/bun/bin/bun"; // default: "bun"
    options.Timeout = TimeSpan.FromSeconds(8);
});
```

`node` or `bun` must be installed on the production host and available on `PATH`
unless an absolute `ExecutablePath` is supplied. The engine verifies the CLI when it
is first resolved from dependency injection and throws an actionable error if it
cannot run it.

During an HTTP render, relative fetches are resolved against an address reported by
the running ASP.NET server, never the incoming `Host` header. Set `BaseUrl` to a
trusted absolute application origin when automatic server addresses are unsuitable
(for example, behind a proxy or with Unix sockets). SvelteNet forwards incoming
`Authorization` and `Cookie` headers to that trusted origin by default, so
authenticated remote queries retain the request identity; edit or clear
`ForwardHeaders` to change that policy. Unlike Jint's in-process bridge, these
backends make a real loopback HTTP request.

## Custom renderer

Implement `ISvelteSsrEngine` when JavaScript should run in a persistent sidecar,
worker pool, embedded runtime, remote service, or any other host:

```csharp
public sealed class MyRenderer : ISvelteSsrEngine
{
    public SsrResult Render(
        string componentModule,
        string renderModule,
        string? propsJson,
        CancellationToken cancellationToken = default)
    {
        // Execute the modules and return their head/body output.
        throw new NotImplementedException();
    }
}

builder.Services
    .AddSvelteNet()
    .AddCustomRenderer<MyRenderer>();
```

An existing instance or DI factory also works:

```csharp
builder.Services.AddSvelteNet().AddCustomRenderer(rendererInstance);

builder.Services
    .AddSvelteNet()
    .AddCustomRenderer(services => new MyRenderer(
        services.GetRequiredService<MyRuntime>()));
```

All renderer registrations are singletons. A custom renderer must therefore be
thread-safe or manage its own worker/engine pool.

## Build and deployment

One `vite build` emits the public client assets to `wwwroot/client` and the fully
bundled ESM server output to `.svelte-net/server`. The latter contains no runtime
`node_modules` dependency and must remain private. Deploy it when an SSR renderer is
selected; a client-only application does not execute it.

`SvelteOptions.ServerOutput` and the Vite plugin's `serverOutDir` must match if either
default is changed. See [Options](options.md).
