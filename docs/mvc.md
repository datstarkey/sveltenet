# MVC / arbitrary views

Put `[SvelteComponent]` on the view model and the component is resolved from the type — no strings in views, the model's ambient TypeScript interface lands in `.svelte-net/types/models.d.ts`, and the component file is scaffolded on the next build:

```csharp
[SvelteComponent]                       // → "Components/Cart" (type name minus ViewModel/Model/Dto)
public record CartViewModel(List<Item> Items);

[SvelteComponent("Widgets/Special")]    // or name the path explicitly
public record SpecialViewModel(...);
```

```cshtml
@model CartViewModel
@section Head { @await Html.SvelteHeadAsync(Model) }
@await Html.SvelteAsync(Model)
```

Use the async helpers, as you would `Html.PartialAsync`. A server render can await remote
queries, so there is no correct synchronous equivalent; the sync `Html.Svelte(...)`
overloads work only when SSR is off for that component and otherwise throw rather than
blocking a request thread.

```svelte
<script lang="ts">
    let { data }: { data: CartViewModel } = $props();
</script>
```

The string-based form also works for one-offs — `@await Html.SvelteAsync("Components/Cart", Model.Cart)` — where the model is passed as the component's `data` prop and you write the interface yourself.

Renders are cached per request so a `Svelte`/`SvelteHead` pair renders once. To render the same component multiple times on a page, give each instance a distinct `elementId` (it keys the cache and the container div id) — without one, the second render throws rather than silently reusing the first instance's output:

```cshtml
@await Html.SvelteAsync("Components/Card", Model.ProductA, elementId: "card-a")
@await Html.SvelteAsync("Components/Card", Model.ProductB, elementId: "card-b")
```

A runnable version of this setup lives in `samples/MvcHello`.
