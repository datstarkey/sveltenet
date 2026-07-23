<script lang="ts">
	import { createTodo, getStats, getTodos, toggleTodo } from './remote';

	// Queries are cached and deduped: getTodos() === getTodos().
	const todos = getTodos();
	const stats = getStats();
</script>

<svelte:head>
	<title>Remote functions</title>
</svelte:head>

<main>
	<h1>Remote functions</h1>

	{#if todos.loading}
		<p>loading…</p>
	{:else if todos.error}
		<p class="error">failed to load</p>
	{:else}
		<ul>
			{#each todos.current ?? [] as todo (todo.id)}
				<li class={{ done: todo.done }}>
					<!-- command + single-flight-style refresh of both queries -->
					<button onclick={() => toggleTodo(todo.id).updates(todos, stats)}>
						{todo.done ? '☑' : '☐'}
					</button>
					<span>{todo.label}</span>
					<em>{todo.priority}</em>
				</li>
			{/each}
		</ul>
	{/if}

	{#if stats.current}
		<p>{stats.current.completed} / {stats.current.total} done</p>
	{/if}

	<!-- [Form] remote function: spread onto the form, fields drive inputs and issues -->
	<form {...createTodo}>
		{#each createTodo.fields.label.issues() ?? [] as issue}
			<p class="error">{issue.message}</p>
		{/each}
		<input {...createTodo.fields.label.as('text')} placeholder="What needs doing?" />
		<select {...createTodo.fields.priority.as('select')}>
			<option>Low</option>
			<option selected>Medium</option>
			<option>High</option>
		</select>
		<button disabled={!!createTodo.pending}>Add</button>
	</form>

	{#if createTodo.result}
		<p>Added "{createTodo.result.label}"!</p>
	{/if}
</main>

<style>
	main {
		max-width: 40rem;
		margin: 2rem auto;
		font-family: system-ui, sans-serif;
	}

	.error {
		color: #c00;
	}

	ul {
		list-style: none;
		padding: 0;
	}

	li {
		display: flex;
		align-items: center;
		gap: 0.5rem;
	}

	li.done span {
		text-decoration: line-through;
		opacity: 0.6;
	}

	em {
		margin-left: auto;
		opacity: 0.5;
	}
</style>
