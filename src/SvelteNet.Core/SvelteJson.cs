namespace SvelteNet;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The one serialization contract shared by props, remote functions, and the
/// TypeScript generator: camelCase properties, dictionary keys, and enum values,
/// plus lossless 64-bit numerics.
/// </summary>
public static class SvelteJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			new LargeNumberConverter<long>(),
			new LargeNumberConverter<ulong>(),
			new LargeNumberConverter<decimal>()
		}
	};
}

/// <summary>
/// Writes long/ulong/decimal as JSON strings, and reads either strings or numbers.
///
/// JavaScript numbers are IEEE-754 doubles: integers above 2^53 and decimal fractions
/// cannot round-trip through them. Emitting these as JSON numbers silently corrupts
/// snowflake IDs, tick counts, and money the moment they exceed that range — with no
/// error anywhere. Strings preserve the value exactly; the TypeScript generator types
/// these as <c>string</c> so the contract stays honest on both sides.
///
/// Reading accepts numbers too, so payloads produced by other clients still bind.
/// </summary>
internal sealed class LargeNumberConverter<T> : JsonConverter<T>
	where T : struct
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var text = reader.GetString();
			if (text is null) throw new JsonException($"Cannot convert null to {typeof(T).Name}.");
			return Parse(text);
		}

		if (reader.TokenType == JsonTokenType.Number)
		{
			if (typeof(T) == typeof(long)) return (T)(object)reader.GetInt64();
			if (typeof(T) == typeof(ulong)) return (T)(object)reader.GetUInt64();
			return (T)(object)reader.GetDecimal();
		}

		throw new JsonException($"Unexpected token {reader.TokenType} when reading {typeof(T).Name}.");
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		writer.WriteStringValue(Format(value));

	/// <summary>Dictionary keys are already strings on the wire; keep the same text form.</summary>
	public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		Parse(reader.GetString() ?? throw new JsonException($"Cannot convert null to {typeof(T).Name}."));

	public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		writer.WritePropertyName(Format(value));

	private static T Parse(string text)
	{
		if (typeof(T) == typeof(long)) return (T)(object)long.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
		if (typeof(T) == typeof(ulong)) return (T)(object)ulong.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
		return (T)(object)decimal.Parse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
	}

	private static string Format(T value) =>
		value switch
		{
			long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
			ulong u => u.ToString(System.Globalization.CultureInfo.InvariantCulture),
			decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
			_ => value.ToString() ?? string.Empty
		};
}
