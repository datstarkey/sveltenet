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
            throw new RemoteInvalidException(nameof(label), "A label is required.");
        // ...
    }
}
```

The scaffolder generates a typed client (`Svelte/remote.ts`) and **SvelteNet.Generators** (a Roslyn source generator) compiles the dispatchers — argument binding and invocation are generated code, no reflection (a reflection fallback covers projects without the analyzer):

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

`refresh()` (and `command(...).updates(...)`) re-runs the awaits automatically. Caveat: the SSR engine has no `fetch`, so await-expression components must disable SSR — override `SvelteComponentOptions()` on the page model and set `options.Ssr = false` (see `samples/TodoApp/Pages/Remote.cshtml.cs`). An in-process fetch bridge to run queries during SSR is planned.
