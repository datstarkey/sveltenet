# Forms & enhance (actions)

The `data` prop always includes `antiforgeryToken` and `problem` — an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) validation problem (`{ title, status, errors }`, where `errors` is the standard ASP.NET `Record<string, string[]>` member), or `null` when the model state is valid. Razor Pages named handlers work as normal form actions — `action="?handler=toggle"` maps to `OnPostToggle(int id)`.

Without JavaScript, forms are plain POSTs with full page reloads. Attach `enhance` for SvelteKit-style progressive enhancement — the submit goes over `fetch` and the island updates in place:

```svelte
<script lang="ts">
    import { enhance } from 'sveltenet/client';

    let { data: serverData }: { data: IndexData } = $props();
    // Writable $derived: tracks the server prop, assignable from enhanced responses.
    let data = $derived(serverData);

    const applyUpdate = enhance({ onUpdate: (d: IndexData) => (data = d) });
</script>

<form method="post" {@attach applyUpdate}>
    <input type="hidden" name="__RequestVerificationToken" value={data.antiforgeryToken} />
    <input name="NewLabel" />
    <button>Add</button>
</form>
```

How it works: `enhance` submits with an `X-SvelteNet` header. `SveltePage` answers enhanced requests with JSON instead of HTML:

- Handler returns `Page()` with valid model state → the fresh props bag (`{ data }`).
- Handler returns `Page()` with **invalid** model state → `400 application/problem+json`: an RFC 9457 problem details with the `errors` member, plus a `data` extension member carrying the fresh props — `onUpdate` still fires, and the island reads the errors from `data.problem`. Any problem-details-aware client parses the same response.
- Handler redirects (post/redirect/get) → `{ "redirect": url }`; the client follows it with a second enhanced fetch and applies the fresh data (then resets the form; pass `reset: false` to keep values).
- Anything else (JSON, files, status codes) passes through untouched.
- A plain `GET` with the `X-SvelteNet` header returns the page's `data` as JSON — useful for polling or manual refreshes.

Enhanced forms degrade gracefully: no JS (or an `onError` you handle) means the normal POST flow. Override `CreateEnhancedResult` on your page model to customize the protocol. See `samples/TodoApp` for the full add/toggle/validation wiring.
