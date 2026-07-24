<script lang="ts">
	import { clientPending } from 'sveltenet/remote';
	import { TodoApi } from './TodoApi.remote';

	// Queries are cached and deduped: TodoApi.GetTodos() === TodoApi.GetTodos().
	const todos = TodoApi.GetTodos();
	const stats = TodoApi.GetStats();

	// FluentValidation demo: commands reject with the same issues shape.
	let feedbackMessage = $state('');
	let feedbackRating = $state(5);
	let feedbackIssues = $state<{ message: string }[]>([]);
	let feedbackResult = $state('');

	async function sendFeedback() {
		feedbackIssues = [];
		try {
			feedbackResult = await TodoApi.SubmitFeedback({ message: feedbackMessage, rating: feedbackRating });
			feedbackMessage = '';
		} catch (error) {
			feedbackIssues = Object.values((error as { issues?: Record<string, { message: string }[]> }).issues ?? {}).flat();
		}
	}
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
					<button onclick={() => TodoApi.ToggleTodo(todo.id).updates(todos, stats)}>
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
	<form {...TodoApi.CreateTodo}>
		{#each TodoApi.CreateTodo.fields.label.issues() ?? [] as issue}
			<p class="error">{issue.message}</p>
		{/each}
		<input {...TodoApi.CreateTodo.fields.label.as('text')} placeholder="What needs doing?" />
		<select {...TodoApi.CreateTodo.fields.priority.as('select')}>
			<option>Low</option>
			<option selected>Medium</option>
			<option>High</option>
		</select>
		<button disabled={!!TodoApi.CreateTodo.pending}>Add</button>
	</form>

	{#if TodoApi.CreateTodo.result}
		<p>Added "{TodoApi.CreateTodo.result.label}"!</p>
	{/if}

	<!-- [EmailAddress] on the C# parameter validates automatically (BYOV pipeline) —
	     the frontend code is identical to any other validation failure -->
	<form {...TodoApi.Subscribe}>
		{#each TodoApi.Subscribe.fields.email.issues() ?? [] as issue}
			<p class="error">{issue.message}</p>
		{/each}
		<input {...TodoApi.Subscribe.fields.email.as('text')} placeholder="you@example.com" />
		<button disabled={!!TodoApi.Subscribe.pending}>Subscribe</button>
	</form>

	{#if TodoApi.Subscribe.result}
		<p>Subscribed {TodoApi.Subscribe.result}!</p>
	{/if}

	<!-- FluentValidation (FeedbackValidator in C#) runs automatically before the
	     command executes; failures arrive as the same problem-details issues -->
	<div class="feedback">
		{#each feedbackIssues as issue}
			<p class="error">{issue.message}</p>
		{/each}
		<input bind:value={feedbackMessage} placeholder="Any feedback?" />
		<select bind:value={feedbackRating}>
			{#each [1, 2, 3, 4, 5] as rating (rating)}
				<option value={rating}>{rating}★</option>
			{/each}
		</select>
		<button onclick={sendFeedback}>Send</button>
		{#if feedbackResult}<span>{feedbackResult}</span>{/if}
	</div>
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
