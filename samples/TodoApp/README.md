# TodoApp

The classic SvelteNet island: a Razor Pages `SveltePage` passes typed `[SvelteProp]` props to `Svelte/Index.svelte`, forms post back to page handlers, and `enhance()` upgrades them to no-reload submissions that re-render from fresh JSON props.

Shows: `[SvelteProp]` → generated TypeScript types, `data.problem` validation errors, antiforgery wiring, writable `$derived` for optimistic prop updates, and explicit in-process SSR with `AddJintSSR()`.
