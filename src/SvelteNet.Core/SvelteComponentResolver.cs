namespace SvelteNet;

using System.Collections.Concurrent;
using System.Reflection;

/// <summary>Maps [SvelteComponent] model types to their component paths.</summary>
public static class SvelteComponentResolver
{
	private static readonly string[] TrimmedSuffixes = ["ViewModel", "Model", "Dto"];
	private static readonly ConcurrentDictionary<Type, string> Cache = new();

	public static string Resolve(Type type) => Cache.GetOrAdd(type, static t =>
	{
		var attribute = t.GetCustomAttribute<SvelteComponentAttribute>()
			?? throw new InvalidOperationException(
				$"'{t.Name}' has no [SvelteComponent] attribute. Add one to render it with Html.Svelte(model), " +
				"or name the component explicitly with Html.Svelte(\"path\", model).");

		return attribute.Component ?? $"Components/{TrimSuffix(t.Name)}";
	});

	private static string TrimSuffix(string name)
	{
		foreach (var suffix in TrimmedSuffixes)
		{
			if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
				return name[..^suffix.Length];
		}

		return name;
	}
}
