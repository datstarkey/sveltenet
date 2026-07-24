namespace SvelteNet.Generated;

using System.Collections.Concurrent;
using System.Reflection;

/// <summary>A generated page prop accessor and its client-visible TypeScript type.</summary>
public sealed record SvelteGeneratedProperty(
	string Name,
	string JsonName,
	Type Type,
	string TypeScriptType,
	bool IsNullable,
	string[] Imports,
	Func<object, object?> GetValue);

/// <summary>Generated metadata for one SveltePage type.</summary>
public sealed record SvelteGeneratedPage(
	Assembly OwnerAssembly,
	Type PageType,
	string Component,
	SvelteGeneratedProperty[] Properties);

/// <summary>Generated metadata for one [SvelteComponent] view model.</summary>
public sealed record SvelteGeneratedComponent(
	Assembly OwnerAssembly,
	Type ModelType,
	string Component);

/// <summary>A serializer-aware TypeScript declaration generated from Roslyn symbols.</summary>
public sealed record SvelteGeneratedType(
	Assembly OwnerAssembly,
	Type Type,
	string TypeScriptName,
	string Declaration);

/// <summary>
/// Process-wide registration point populated by source-generated module initializers.
/// Consumers always filter by the application's explicitly scoped assemblies.
/// </summary>
public static class SvelteGeneratedMetadata
{
	private static readonly ConcurrentDictionary<Type, SvelteGeneratedPage> Pages = new();
	private static readonly ConcurrentDictionary<Type, SvelteGeneratedComponent> Components = new();
	private static readonly ConcurrentDictionary<(Assembly Owner, Type Type), SvelteGeneratedType> Types = new();

	public static void RegisterPage(SvelteGeneratedPage descriptor) => Pages[descriptor.PageType] = descriptor;

	public static void RegisterComponent(SvelteGeneratedComponent descriptor) => Components[descriptor.ModelType] = descriptor;

	public static void RegisterType(SvelteGeneratedType descriptor) => Types[(descriptor.OwnerAssembly, descriptor.Type)] = descriptor;

	public static bool TryGetPage(Type pageType, out SvelteGeneratedPage descriptor)
	{
		for (var current = pageType; current is not null; current = current.BaseType)
		{
			if (Pages.TryGetValue(current, out descriptor!)) return true;
		}
		descriptor = null!;
		return false;
	}

	public static bool TryGetComponent(Type modelType, out SvelteGeneratedComponent descriptor) =>
		Components.TryGetValue(modelType, out descriptor!);

	public static IReadOnlyList<SvelteGeneratedPage> GetPages(IReadOnlyList<Assembly>? assemblies) =>
		Pages.Values.Where(p => InScope(p.OwnerAssembly, assemblies)).ToArray();

	public static IReadOnlyList<SvelteGeneratedComponent> GetComponents(IReadOnlyList<Assembly>? assemblies) =>
		Components.Values.Where(c => InScope(c.OwnerAssembly, assemblies)).ToArray();

	public static IReadOnlyList<SvelteGeneratedType> GetTypes(IReadOnlyList<Assembly>? assemblies) =>
		Types.Values.Where(t => InScope(t.OwnerAssembly, assemblies)).ToArray();

	private static bool InScope(Assembly owner, IReadOnlyList<Assembly>? assemblies) =>
		assemblies is null or { Count: 0 } || assemblies.Contains(owner);
}
