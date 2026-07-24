# Remote functions

SvelteKit-style typed RPC to C# — no fetch routes. Mark a class `[SvelteRemote]` and its methods `[Query]`, `[Command]`, or `[Form]`:

```csharp
[SvelteRemote]
public class TodoApi(TodoStore store)
{
    [Query]   public IReadOnlyList<Todo> GetTodos() => store.All;
    [Command] public void ToggleTodo(int id) => store.Toggle(id);
    [Form]    public Task<Todo> CreateTodo(string label, Priority priority)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new SvelteValidationException(nameof(label), "A label is required.");
        // ...
    }
}
```

The scaffolder generates a typed client (`Svelte/remote.ts`) and **SvelteNet.Generators** (a Roslyn source generator) compiles the dispatchers — argument binding, invocation, *and registration* are generated code with no reflection: each service's descriptor self-registers via a `[ModuleInitializer]`, and `AddSvelteNet()` consumes those descriptors, scoped to the calling assembly (see [Options → Discovery scope](options.md#discovery-scope)). A reflection fallback covers projects without the analyzer. The full flow lives in `samples/RemoteFunctions`:

```svelte
<script lang="ts">
    import { createTodo, getStats, getTodos, toggleTodo } from './remote';

    const todos = getTodos();     // cached + deduped: getTodos() === getTodos()
</script>

{#if todos.loading}<p>loading…</p>
{:else}
    {#each todos.current ?? [] as todo (todo.id)}
        <button onclick={() => toggleTodo(todo.id).updates(todos, getStats())}>toggle</button>
    {/each}
{/if}

<form {...createTodo}>
    {#each createTodo.fields.label.issues() ?? [] as issue}<p>{issue.message}</p>{/each}
    <input {...createTodo.fields.label.as('text')} />
    <button disabled={!!createTodo.pending}>Add</button>
</form>
{#if createTodo.result}<p>Added!</p>{/if}
```

Semantics mirror SvelteKit: queries are GET, cached, with `current`/`loading`/`error`, `refresh()`, and `set()`; commands are POST JSON promises with `.updates(...queries)`; forms spread onto `<form>`, expose `fields` (`as()`, `issues()`, `value()`, `set()`), `pending`, `result`, `validate()`, `enhance(cb)` with `form.submit()`, and `for(id)` for lists — and post/redirect without JavaScript. Successful form submits refresh all page queries. Endpoints live under `/_sveltenet/remote` (customize/authorize via `MapSvelteRemote`).

**Validation is standard problem details** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)): binding failures, validator errors, and `SvelteValidationException` (field → message, the SvelteKit `invalid(...)` equivalent) all become a `400 application/problem+json` response with the ASP.NET `errors` member (`Results.ValidationProblem`), so any problem-details-aware tooling parses it. The client maps `errors` onto each field's `issues()`; errors with an empty field name are form-level and appear in `fields.allIssues()`. `X-SvelteNet-Validate` runs binding + validators only (204 when valid, problem details when not).

**Validation runs automatically** (BYOV — bring your own validation). Registered `ISvelteRemoteValidator`s run after binding and before your method executes, for queries, commands, and forms alike. DataAnnotations work out of the box — on parameters and on the properties of complex argument types — with no imperative code:

```csharp
[Form] public string Subscribe([EmailAddress] string email) => email;
```

An invalid email never reaches the method body; the client shows it on `fields.email.issues()` exactly like any other error. Plug in anything else by registering more validators — e.g. a FluentValidation adapter:

```csharp
builder.Services.AddSingleton<ISvelteRemoteValidator, FluentValidationRemoteValidator>();

public class FluentValidationRemoteValidator(IServiceProvider services) : ISvelteRemoteValidator
{
    public async ValueTask ValidateAsync(RemoteValidationContext context)
    {
        foreach (var (name, value) in context.Arguments)
        {
            if (value is null || services.GetService(typeof(IValidator<>).MakeGenericType(value.GetType())) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(value), context.CancellationToken);
            foreach (var failure in result.Errors)
                context.AddError(JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName), failure.ErrorMessage);
        }
    }
}
```

The frontend never changes: whichever validator produced the error, it arrives as the same problem details and lights up the same `issues()` / `aria-invalid` / `validate()` machinery. Razor Pages get the equivalent for free through `ModelState` — anything that writes to it (DataAnnotations, FluentValidation auto-validation, manual `AddModelError`) flows into `data.problem`.

## Await expressions (experimental)

Enable Svelte's experimental async support through the plugin and await queries directly in components, with `<svelte:boundary>` providing the pending state:

```ts
// vite.config.ts
sveltenet({ experimentalAsync: true })
```

```svelte
<svelte:boundary>
	{#snippet pending()}<p>loading…</p>{/snippet}

	{#each await getTodos() as todo (todo.id)}
		<li>{todo.label}</li>
	{/each}
</svelte:boundary>
```

`refresh()` (and `command(...).updates(...)`) re-runs the awaits automatically.

**SSR**: an in-process fetch bridge (`ISvelteSsrFetchHandler` / `RemoteSsrFetchHandler`) routes `fetch` calls made inside the SSR engine straight to the [Query] descriptors — no HTTP round-trip — and the engine serves a `node:async_hooks` shim so Svelte's async server runtime loads.

**Fully awaited SSR + hydration stash**: a static `{#snippet pending()}` makes Svelte's compiler DROP the boundary's children from the server bundle (pending always renders, even in Node). Pass the snippet through `clientPending` instead:

```svelte
{#snippet loading()}<p>loading…</p>{/snippet}
<svelte:boundary pending={clientPending(loading)}>
	{#each await getTodos() as todo (todo.id)}...{/each}
</svelte:boundary>
```

The server then awaits the queries (through the bridge) and renders the real HTML, and query results are stashed via Svelte's `hydratable` into the page head (`window.__svelte.h`) — hydration adopts them with **zero network requests**. `refresh()` always performs a real fetch.

On the client, query instances are cached (`getTodos() === getTodos()` per argument set). On the server they are deliberately **not** — SSR engines are pooled and keep the module graph alive across requests, so a cached instance would serve the first render's data forever and skip the hydration stash. `hydratable` still dedupes fetches within a single render by key.

Queries are reactive thenables: `then`/`catch`/`finally` are property getters that read internal state, because `await` only touches Svelte's dependency tracking during the synchronous `.then` property access. That's what makes `{#each await getTodos() ...}` re-render after `refresh()`/`.updates()`/`set()` — including after a hydratable-adopted hydration.
