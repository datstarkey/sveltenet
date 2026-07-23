namespace SvelteNet;

/// <summary>
/// Marks a page-model property as part of the data passed to the Svelte component.
/// Marked properties are serialized into the component's "data" prop and included
/// in the generated TypeScript types.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SveltePropAttribute : Attribute
{
}
