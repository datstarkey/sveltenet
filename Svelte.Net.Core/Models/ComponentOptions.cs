namespace Svelte.Net.Core.Models
{
	public class ComponentOptions
	{
		public string? Id { get; set; }
		public string? Path { get; set; }
		public string PagesPath { get; set; } = "Routes";
		public bool Ssr { get; set; } = true;
		public bool Csr { get; set; } = true;
		public bool Prerender { get; set; } = false;
		public object? Data { get; set; }
	}
}
