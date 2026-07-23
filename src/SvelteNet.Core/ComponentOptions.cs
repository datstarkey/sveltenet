namespace SvelteNet;

public class ComponentOptions
{
	/// <summary>Component path relative to the pages directory, without extension. E.g. "Index" or "Admin/Users".</summary>
	public required string Component { get; init; }

	/// <summary>Id of the container element. Defaults to a slug derived from <see cref="Component"/>.</summary>
	public string? ElementId { get; set; }

	/// <summary>The full props bag passed to the component. Serialized with <see cref="SvelteRenderer.JsonOptions"/>.</summary>
	public object? Props { get; set; }

	/// <summary>Overrides <see cref="SvelteOptions.EnableSsr"/> for this component when set.</summary>
	public bool? Ssr { get; set; }

	/// <summary>Overrides <see cref="SvelteOptions.EnableCsr"/> for this component when set.</summary>
	public bool? Csr { get; set; }
}
