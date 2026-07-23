namespace SvelteNet;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The one serialization contract shared by props, remote functions, and the
/// TypeScript generator: camelCase properties, dictionary keys, and enum values.
/// </summary>
public static class SvelteJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};
}
