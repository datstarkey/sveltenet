namespace SvelteNet;

/// <summary>
/// Names this type in generated TypeScript. Without it the short CLR name is used,
/// qualified with namespace segments only when that would collide with another model
/// type or shadow a built-in DOM/TypeScript global.
///
/// Use it when the automatic name is not what you want:
///     [SvelteType("CatalogItem")] public class Item { }
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum)]
public sealed class SvelteTypeAttribute : Attribute
{
	public SvelteTypeAttribute(string name) => Name = name;

	/// <summary>The exact interface/type name to emit.</summary>
	public string Name { get; }
}
