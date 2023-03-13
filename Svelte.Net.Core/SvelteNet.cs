namespace Svelte.Net.Core
{
	using Models;
	using System;

	public static class SvelteNet
	{
		public static SvelteOptions Options { get; set; } = new ();
	
		public static void Configure(Action<SvelteOptions>? options = null)
		{
			options?.Invoke(Options);
		}

		public static SvelteService CreateService(Action<SvelteOptions>? options = null)
		{
			if (options is null)
			{
				return new SvelteService(Options);
			}

			var serviceOptions = Options.Clone();
			options.Invoke(serviceOptions);
			return new SvelteService(serviceOptions);
		}

	}
}
