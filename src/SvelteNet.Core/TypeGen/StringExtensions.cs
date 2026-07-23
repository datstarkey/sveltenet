namespace SvelteNet.TypeGen;

using System.Text.Json;

public static class StringExtensions
{
	/// <summary>
	/// Camel-cases via <see cref="JsonNamingPolicy.CamelCase"/> so generated TypeScript
	/// names always match what the serializer emits (e.g. "ID" → "id", not "iD").
	/// </summary>
	public static string ToCamelCase(this string str) =>
		string.IsNullOrEmpty(str) ? str : JsonNamingPolicy.CamelCase.ConvertName(str);

	/// <summary>Strips the generic arity suffix from a CLR type name ("Paged`1" → "Paged").</summary>
	public static string RemoveTypeArity(this string value) => value.Split('`')[0];
}
