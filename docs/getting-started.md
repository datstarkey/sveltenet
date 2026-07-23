# Getting started

```csharp
// Program.cs
using SvelteNet.AspNetCore;

builder.Services.AddRazorPages();
builder.Services.AddSvelteNet();          // options: o => o.PagesPath = "Svelte", ...

var app = builder.Build();
app.UseStaticFiles();
app.UseSvelteNet();                       // remote endpoints (+ optional startup scaffolding)
app.MapRazorPages();
```

```csharp
// Pages/Index.cshtml.cs
using SvelteNet;
using SvelteNet.AspNetCore;

public class IndexModel : SveltePage
{
    [SvelteProp] public string Title { get; set; } = "Hello";
    [SvelteProp] public List<Todo> Todos { get; set; } = [];
}
```

```cshtml
@* Pages/Index.cshtml *@
@model IndexModel
@section Head { @Model.SvelteHead() }
@Model.Svelte()
```

`dotnet build` generates the frontend types (the `SvelteNet.Build.targets` MSBuild target runs the scaffolder against the built assembly — no app run needed, and `dotnet watch` regenerates on every rebuild):

```
Svelte/
  types.ts            ← generated: TS interfaces for all [SvelteProp] model types
  routes.d.ts         ← generated: RouteId union of your page routes
  Index.types.ts      ← generated: interface IndexData { title: string; todos: Todo[]; ... }
  Index.svelte        ← scaffolded once, then yours
  mount.ts            ← one-line re-export of sveltenet/client (stable manifest key)
  render.ts           ← one-line re-export of sveltenet/server
vite.config.ts        ← scaffolded once: plugins: [sveltenet()]
package.json          ← scaffolded once
```

```svelte
<script lang="ts">
    import type { IndexData } from './Index.types';

    let { data }: { data: IndexData } = $props();
</script>
```

Then `npm install` (or bun/pnpm) and:

- **Development**: `dotnet watch run` + `npm run dev` in two terminals (see below).
- **Production**: `npm run build` (client + SSR bundles), then deploy. `wwwroot/client` is served statically; `svelte-ssr` is executed by Jint and is deliberately *not* under wwwroot.
