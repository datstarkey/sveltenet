<script lang="ts">
	import { WeatherApi } from './WeatherApi.remote';

	const forecasts = WeatherApi.GetForecasts();
	let refreshed = $state('');

	async function refresh() {
		refreshed = await WeatherApi.RefreshForecasts().updates(forecasts);
	}
</script>

<main>
	<h1>Weather feature</h1>
	<p>This island and its generated remote client are colocated with <code>WeatherApi.cs</code>.</p>

	{#if forecasts.loading}
		<p>Loading forecast…</p>
	{:else}
		<ul>
			{#each forecasts.current ?? [] as forecast (forecast.day)}
				<li><strong>{forecast.day}</strong>: {forecast.temperatureC}°C, {forecast.summary}</li>
			{/each}
		</ul>
	{/if}

	<button onclick={refresh}>Refresh</button>
	{#if refreshed}<p>{refreshed}</p>{/if}
	<p><a href="/">Back to todos</a></p>
</main>
