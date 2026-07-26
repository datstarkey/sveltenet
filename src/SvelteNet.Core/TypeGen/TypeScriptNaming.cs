namespace SvelteNet.TypeGen;

using System.Reflection;
using System.Text;

/// <summary>
/// Decides what a CLR model type is called in generated TypeScript.
///
/// Naming happens in two stages because no single generator can see the whole app. The
/// source generators and the reflection fallback emit a <em>placeholder</em> derived from
/// the full CLR name (<c>Orders_Item</c>), which is unique by construction. The scaffolder
/// runs last, across every application assembly, and is therefore the only component that
/// can tell whether the short name <c>Item</c> is actually free — so it resolves the
/// placeholders to their final names and substitutes them throughout the generated output.
/// </summary>
public static class TypeScriptNaming
{
	/// <summary>
	/// Names declared by lib.dom.d.ts and the TypeScript standard library that a generated
	/// ambient interface would otherwise shadow across the entire project. A C# class called
	/// File or Event is perfectly ordinary; silently replacing the DOM's is not.
	/// </summary>
	public static readonly IReadOnlySet<string> ReservedGlobals = new HashSet<string>(StringComparer.Ordinal)
	{
		"Array", "Blob", "Boolean", "Comment", "Date", "Document", "Element", "Error", "Event",
		"File", "FormData", "Function", "Headers", "History", "Image", "Location", "Map",
		"Node", "Notification", "Number", "Object", "Option", "Promise", "Range", "Record",
		"Report", "Request", "Response", "Selection", "Set", "State", "Storage", "String",
		"Symbol", "Text", "URL", "Window", "WeakMap", "WeakSet"
	};

	private const char Separator = '_';

	/// <summary>The placeholder a generator emits for a type: its namespace path plus name.</summary>
	public static string Placeholder(Type type)
	{
		var name = type.Name.RemoveTypeArity();
		var ns = type.Namespace;
		return string.IsNullOrEmpty(ns) ? name : ns!.Replace('.', Separator) + Separator + name;
	}

	/// <summary>
	/// Resolves placeholders to the shortest name that is unambiguous and does not shadow a
	/// built-in global. Explicit names (from [SvelteType]) win outright and are reserved
	/// first, so an automatic name never steals one.
	/// </summary>
	/// <param name="placeholders">Every placeholder that will appear in the generated output.</param>
	/// <param name="explicitNames">Placeholder → name chosen with [SvelteType].</param>
	public static IReadOnlyDictionary<string, string> Resolve(
		IEnumerable<string> placeholders,
		IReadOnlyDictionary<string, string>? explicitNames = null)
	{
		var all = placeholders.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
		var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
		var taken = new HashSet<string>(StringComparer.Ordinal);

		foreach (var placeholder in all)
		{
			if (explicitNames is null || !explicitNames.TryGetValue(placeholder, out var chosen)) continue;
			resolved[placeholder] = chosen;
			taken.Add(chosen);
		}

		// A short name is only safe if exactly one placeholder wants it.
		var shortNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var placeholder in all)
		{
			if (resolved.ContainsKey(placeholder)) continue;
			var candidate = Segments(placeholder)[^1];
			shortNameCounts[candidate] = shortNameCounts.TryGetValue(candidate, out var count) ? count + 1 : 1;
		}

		foreach (var placeholder in all)
		{
			if (resolved.ContainsKey(placeholder)) continue;
			resolved[placeholder] = Disambiguate(placeholder, shortNameCounts, taken);
		}

		return resolved;
	}

	private static string Disambiguate(string placeholder, Dictionary<string, int> shortNameCounts, HashSet<string> taken)
	{
		var segments = Segments(placeholder);

		// Grow rightward from the bare name: Item, OrdersItem, CatalogOrdersItem, ...
		for (var take = 1; take <= segments.Length; take++)
		{
			var candidate = Join(segments, take);
			var isBareName = take == 1;
			if (isBareName && (shortNameCounts.GetValueOrDefault(candidate) > 1 || ReservedGlobals.Contains(candidate)))
				continue;
			if (!isBareName && ReservedGlobals.Contains(candidate)) continue;
			if (taken.Add(candidate)) return candidate;
		}

		// Every qualification was taken — fall back to the placeholder, which is unique.
		taken.Add(placeholder);
		return placeholder;
	}

	private static string[] Segments(string placeholder) => placeholder.Split(Separator);

	private static string Join(string[] segments, int take)
	{
		var builder = new StringBuilder();
		for (var i = segments.Length - take; i < segments.Length; i++) builder.Append(segments[i]);
		return builder.ToString();
	}

	/// <summary>
	/// Replaces placeholders with their resolved names. Matching is whole-identifier only,
	/// so a placeholder never rewrites part of a longer name.
	/// </summary>
	public static string Substitute(string source, IReadOnlyDictionary<string, string> resolved)
	{
		if (source.Length == 0 || resolved.Count == 0) return source;

		var builder = new StringBuilder(source.Length);
		var index = 0;
		while (index < source.Length)
		{
			if (!IsIdentifierStart(source[index]))
			{
				builder.Append(source[index++]);
				continue;
			}

			var start = index;
			while (index < source.Length && IsIdentifierPart(source[index])) index++;
			var token = source[start..index];
			builder.Append(resolved.TryGetValue(token, out var replacement) ? replacement : token);
		}

		return builder.ToString();
	}

	private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$';

	private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

	/// <summary>The [SvelteType] name for a type, if it declares one.</summary>
	public static string? ExplicitName(Type type) =>
		type.GetCustomAttribute<SvelteTypeAttribute>(inherit: false)?.Name;
}
