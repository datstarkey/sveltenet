<script lang="ts">
	import { enhance } from 'sveltenet/client';
	import type { IndexData } from './Index.types';

	let { data: serverData }: { data: IndexData } = $props();

	// Writable $derived: tracks the server-rendered prop, but enhanced form
	// responses can assign fresh data without a page reload.
	let data = $derived(serverData);

	const applyUpdate = enhance({ onUpdate: (d: IndexData) => (data = d) });

	// Server state, client-side view logic — this is the island pattern.
	let filter: 'all' | 'active' | 'done' = $state('all');
	let visible = $derived(data.todos.filter((t) => filter === 'all' || (filter === 'done') === t.done));
	let remaining = $derived(data.todos.filter((t) => !t.done).length);
	let errors = $derived(Object.values(data.modelState).flat());
</script>

<svelte:head>
	<title>{data.title}</title>
</svelte:head>

<main>
	<h1>{data.title}</h1>
	<p>{remaining} of {data.todos.length} remaining</p>

	{#each errors as error}
		<p class="error">{error}</p>
	{/each}

	<form method="post" {@attach applyUpdate}>
		<input type="hidden" name="__RequestVerificationToken" value={data.antiforgeryToken} />
		<input name="NewLabel" placeholder="What needs doing?" />
		<select name="NewPriority">
			<option>Low</option>
			<option selected>Medium</option>
			<option>High</option>
		</select>
		<button>Add</button>
	</form>

	<div role="group">
		{#each ['all', 'active', 'done'] as const as option (option)}
			<button class={{ active: filter === option }} onclick={() => (filter = option)}>
				{option}
			</button>
		{/each}
	</div>

	<ul>
		{#each visible as todo (todo.id)}
			<li class={{ done: todo.done }}>
				<form method="post" action="?handler=toggle" {@attach applyUpdate}>
					<input type="hidden" name="__RequestVerificationToken" value={data.antiforgeryToken} />
					<input type="hidden" name="id" value={todo.id} />
					<button type="submit" aria-label="toggle {todo.label}">{todo.done ? '☑' : '☐'}</button>
				</form>
				<span>{todo.label}</span>
				<em>{todo.priority}</em>
			</li>
		{/each}
	</ul>
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
		padding: 0.25rem 0;
	}

	li.done span {
		text-decoration: line-through;
		opacity: 0.6;
	}

	li form {
		display: inline;
	}

	button.active {
		font-weight: bold;
		text-decoration: underline;
	}

	em {
		margin-left: auto;
		opacity: 0.5;
	}
</style>
