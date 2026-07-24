<script lang="ts">
	import { clientPending } from 'sveltenet/remote';
	import { createTodo, getStats, getTodos, subscribe, toggleTodo } from './remote';

	// Queries are cached and deduped: getTodos() === getTodos().
	const todos = getTodos();
	const stats = getStats();
</script>

<svelte:head>
	<title>Remote functions</title>
</svelte:head>

<main>
	<h1>Remote functions</h1>

	{#snippet loading()}
		<p>loading…</p>
	{/snippet}

	<!-- experimental async: await queries directly. clientPending keeps the pending
	     snippet client-only, so SSR awaits the queries (via the in-process fetch
	     bridge) and renders the real content; hydration adopts it from the stash. -->
	<svelte:boundary pending={clientPending(loading)}>

		<ul>
			{#each await todos as todo (todo.id)}
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

		<p>{(await stats).completed} / {(await stats).total} done</p>
	</svelte:boundary>

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

	<!-- [EmailAddress] on the C# parameter validates automatically (BYOV pipeline) —
	     the frontend code is identical to any other validation failure -->
	<form {...subscribe}>
		{#each subscribe.fields.email.issues() ?? [] as issue}
			<p class="error">{issue.message}</p>
		{/each}
		<input {...subscribe.fields.email.as('text')} placeholder="you@example.com" />
		<button disabled={!!subscribe.pending}>Subscribe</button>
	</form>

	{#if subscribe.result}
		<p>Subscribed {subscribe.result}!</p>
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
