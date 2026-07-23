<script lang="ts">
	import type { StatsData } from './Stats.types';

	let { data }: { data: StatsData } = $props();

	let percent = $derived(data.total === 0 ? 0 : Math.round((data.completed / data.total) * 100));
</script>

<svelte:head>
	<title>Stats</title>
</svelte:head>

<main>
	<h1>Stats</h1>
	<p>{data.completed} / {data.total} done ({percent}%)</p>
	<ul>
		{#each Object.entries(data.byPriority) as [priority, count] (priority)}
			<li>{priority}: {count}</li>
		{/each}
	</ul>
</main>

<style>
	main {
		max-width: 40rem;
		margin: 2rem auto;
		font-family: system-ui, sans-serif;
	}
</style>
