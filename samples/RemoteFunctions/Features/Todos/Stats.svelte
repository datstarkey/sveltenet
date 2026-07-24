<script lang="ts">
	import { TodoApi } from './TodoApi.remote';

	let { data }: { data: TodosStatsData } = $props();

	// SSR shows the page props; the query takes over client-side and can refresh.
	const stats = TodoApi.GetStats();

	let total = $derived(stats.current?.total ?? data.total);
	let completed = $derived(stats.current?.completed ?? data.completed);
	let byPriority = $derived(stats.current?.byPriority ?? data.byPriority);
	let percent = $derived(total === 0 ? 0 : Math.round((completed / total) * 100));
</script>

<svelte:head>
	<title>Stats</title>
</svelte:head>

<main>
	<h1>Stats</h1>
	<p>{completed} / {total} done ({percent}%)</p>
	<ul>
		{#each Object.entries(byPriority) as [priority, count] (priority)}
			<li>{priority}: {count}</li>
		{/each}
	</ul>
	<button onclick={() => stats.refresh()} disabled={stats.loading}>refresh</button>
</main>

<style>
	main {
		max-width: 40rem;
		margin: 2rem auto;
		font-family: system-ui, sans-serif;
	}
</style>
