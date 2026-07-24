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
        return Task.FromResult(store.Add(label, priority));
    }
}
```

The scaffolder generates a typed class beside each C# service
(`Features/Todos/TodoApi.remote.ts`
in the sample) and ambient model declarations under `.svelte-net/types`.
**SvelteNet.Generators** compiles argument binding, validation metadata, invocation,
registration, page props, component mappings, and TypeScript declarations. Each
assembly's descriptors self-register through a module initializer; `AddSvelteNet()`
consumes only descriptors in the configured application scope. There is no reflection
scan on the normal path. Apps intentionally built without the analyzer can opt into
`EnableReflectionFallback` (see [Options → Discovery scope](options.md#discovery-scope)).
The full flow lives in `samples/RemoteFunctions`, whose service, models, validator,
store, Svelte components, and generated client are colocated in one vertical slice:

```text
Features/Todos/
  TodoApi.cs
  TodoStore.cs
  Todo.cs
  Feedback.cs
  Index.svelte
  Stats.svelte
  TodoApi.remote.ts
Features/Weather/
  WeatherApi.cs
  Weather.svelte
  WeatherApi.remote.ts
```

```svelte
<script lang="ts">
    import { TodoApi } from './TodoApi.remote';

    const todos = TodoApi.GetTodos(); // cached + deduped
</script>

{#if todos.loading}<p>loading…</p>
{:else}
    {#each todos.current ?? [] as todo (todo.id)}
        <button onclick={() => TodoApi.ToggleTodo(todo.id).updates(todos, TodoApi.GetStats())}>toggle</button>
    {/each}
{/if}

<form {...TodoApi.CreateTodo}>
    {#each TodoApi.CreateTodo.fields.label.issues() ?? [] as issue}<p>{issue.message}</p>{/each}
    <input {...TodoApi.CreateTodo.fields.label.as('text')} />
    <button disabled={!!TodoApi.CreateTodo.pending}>Add</button>
</form>
{#if TodoApi.CreateTodo.result}<p>Added!</p>{/if}
```

Semantics mirror SvelteKit: queries are GET, cached, with `current`/`loading`/`error`, `refresh()`, and `set()`; commands are POST JSON promises with `.updates(...queries)`; forms spread onto `<form>`, expose `fields` (`as()`, `issues()`, `value()`, `set()`), `pending`, `result`, `validate()`, `enhance(cb)` with `form.submit()`, and `for(id)` for lists — and post/redirect without JavaScript. Successful form submits refresh all page queries. Endpoints live under `/_sveltenet/remote` (customize/authorize via `MapSvelteRemote`).

**Validation is standard problem details** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)): binding failures, validator errors, and `SvelteValidationException` (field → message, the SvelteKit `invalid(...)` equivalent) all become a `400 application/problem+json` response with the ASP.NET `errors` member (`Results.ValidationProblem`), so any problem-details-aware tooling parses it. The client maps `errors` onto each field's `issues()`; errors with an empty field name are form-level and appear in `fields.allIssues()`. `X-SvelteNet-Validate` runs binding + validators only (204 when valid, problem details when not).

**Validation runs automatically** (BYOV — bring your own validation). Registered `ISvelteRemoteValidator`s run after binding and before your method executes, for queries, commands, and forms alike. DataAnnotations work out of the box — on parameters and on the properties of complex argument types — with no imperative code:

```csharp
[Form] public string Subscribe([EmailAddress] string email) => email;
```

An invalid email never reaches the method body; the client shows it on `fields.email.issues()` exactly like any other error.

**FluentValidation** plugs in via the `SvelteNet.FluentValidation` package: register the adapter once and your `IValidator<T>`s (resolved from DI per argument type, scoped services welcome) run automatically:

```csharp
builder.Services.AddSvelteNetFluentValidation();
builder.Services.AddScoped<IValidator<Feedback>, FeedbackValidator>();

public record Feedback(string Message, int Rating);

public class FeedbackValidator : AbstractValidator<Feedback>
{
    public FeedbackValidator()
    {
        RuleFor(f => f.Message).NotEmpty().MinimumLength(5);
        RuleFor(f => f.Rating).InclusiveBetween(1, 5);
    }
}

[Command] public string SubmitFeedback(Feedback feedback) => $"{feedback.Rating}★ noted";
```

Property names camelCase into the `errors` member (`Address.City` → `address.city`). Anything else implements `ISvelteRemoteValidator` directly and registers the same way.

The frontend never changes: whichever validator produced the error, it arrives as the same problem details and lights up the same `issues()` / `aria-invalid` / `validate()` machinery. Razor Pages get the equivalent for free through `ModelState` — anything that writes to it (DataAnnotations, FluentValidation auto-validation, manual `AddModelError`) flows into `data.problem`. `samples/RemoteFunctions` demonstrates all three: imperative `SvelteValidationException`, DataAnnotations, and FluentValidation.

## Await expressions (experimental)

Enable Svelte's experimental async support through the plugin and await queries directly in components, with `<svelte:boundary>` providing the pending state:

```ts
// vite.config.ts
sveltenet({ experimentalAsync: true })
```

```svelte
<svelte:boundary>
	{#snippet pending()}<p>loading…</p>{/snippet}

	{#each await TodoApi.GetTodos() as todo (todo.id)}
		<li>{todo.label}</li>
	{/each}
</svelte:boundary>
```

`refresh()` (and `command(...).updates(...)`) re-runs the awaits automatically.

**SSR is opt-in** with `AddJintSSR()`, `AddNodeSSR()`, `AddBunJsSSR()`, or a
custom renderer. Jint's in-process fetch bridge (`ISvelteSsrFetchHandler` /
`RemoteSsrFetchHandler`) routes `fetch` calls straight to the generated `[Query]`
descriptor with no HTTP round-trip, and serves a `node:async_hooks` shim for Svelte's
async server runtime. The Node.js and Bun renderers resolve relative fetches against
the current app and make loopback HTTP requests while forwarding cookies and
authorization. See [SSR renderers](ssr.md).

**Fully awaited SSR + hydration stash**: a static `{#snippet pending()}` makes Svelte's compiler DROP the boundary's children from the server bundle (pending always renders, even in Node). Pass the snippet through `clientPending` instead:

```svelte
{#snippet loading()}<p>loading…</p>{/snippet}
<svelte:boundary pending={clientPending(loading)}>
	{#each await TodoApi.GetTodos() as todo (todo.id)}...{/each}
</svelte:boundary>
```

The server then awaits the queries (through the bridge) and renders the real HTML, and query results are stashed via Svelte's `hydratable` into the page head (`window.__svelte.h`) — hydration adopts them with **zero network requests**. `refresh()` always performs a real fetch.

On the client, query instances are cached (`TodoApi.GetTodos() ===
TodoApi.GetTodos()` per argument set). On the server they are deliberately **not**.
This is essential for pooled/persistent renderers such as Jint, whose module graph
survives across requests: a cached query could otherwise serve the first render's data
forever and skip the hydration stash. `hydratable` still dedupes fetches within one
render by key.

Queries are reactive thenables: `then`/`catch`/`finally` are property getters that read internal state, because `await` only touches Svelte's dependency tracking during the synchronous `.then` property access. That's what makes `{#each await TodoApi.GetTodos() ...}` re-render after `refresh()`/`.updates()`/`set()` — including after a hydratable-adopted hydration.
