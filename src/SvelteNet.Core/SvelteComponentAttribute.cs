namespace SvelteNet;

/// <summary>
/// Binds a view model to its Svelte component, enabling Html.Svelte(model) without
/// naming the component, TypeScript generation for the model, and dev-time
/// scaffolding of the component file.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SvelteComponentAttribute : Attribute
{
	public SvelteComponentAttribute(string? component = null)
	{
		Component = component;
	}

	/// <summary>
	/// Component path relative to the pages directory, e.g. "Components/Cart".
	/// Defaults to "Components/{TypeName}" with a trailing ViewModel/Model/Dto suffix removed.
	/// </summary>
	public string? Component { get; }
}
