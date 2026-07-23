<script lang="ts">
	// MVC components define their own data shape — type generation covers SveltePage models only.
	interface HelloData {
		name: string;
		visits: number;
	}

	let { data }: { data: HelloData } = $props();

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
