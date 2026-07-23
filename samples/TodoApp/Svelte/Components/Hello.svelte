<script lang="ts">
	import type { HelloViewModel } from '../types';

	let { data }: { data: HelloViewModel } = $props();

	let cheers = $state(0);
</script>

<main>
	<h1>Hello {data.name}!</h1>
	<p>You are visitor #{data.visits} (rendered by an MVC controller).</p>
	<button onclick={() => cheers++}>cheer × {cheers}</button>
</main>

<style>
	main {
		max-width: 40rem;
		margin: 2rem auto;
		font-family: system-ui, sans-serif;
	}
</style>
