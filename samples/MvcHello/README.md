# MvcHello

Svelte islands from MVC controllers. `[SvelteComponent]` on `HelloViewModel` binds it to `Svelte/Components/Hello.svelte` by convention, generates its TypeScript interface, and the view renders it with `@Html.Svelte(Model)` — no component name strings. The sample explicitly enables in-process production SSR with `AddJintSSR()`.
