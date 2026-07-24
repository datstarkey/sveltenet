namespace SvelteNet.TypeGen;

using System.Reflection;
using System.Text.Json.Serialization;

public static class TypeScriptExtensions
{
	public static string GetTypescriptInterface(this Type type, bool useGenericTypes = true)
	{
		var exportType = type.StripNullable() ?? type;

		if (exportType.IsEnum)
		{
			var members = exportType.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Select(f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? f.Name.ToCamelCase())
				.Select(s => $"'{s}'");
			return $"export type {exportType.TsType()} = {string.Join(" | ", members)};";
		}
		if (exportType.IsDefined(typeof(JsonConverterAttribute), inherit: true))
			return $"export type {exportType.TsType()} = unknown;";

		exportType = useGenericTypes || !exportType.IsGenericType ? exportType : exportType.GetGenericTypeDefinition();

		var nullability = new NullabilityInfoContext();
		var props = exportType
			.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead && !IsAlwaysIgnored(p))
			.Select(p =>
			{
				var nullable = nullability.Create(p).ReadState == NullabilityState.Nullable;
				var name = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name.ToCamelCase();
				var optional = IsConditionallyIgnored(p) ? "?" : string.Empty;
				var propertyType = p.IsDefined(typeof(JsonConverterAttribute), inherit: true)
					? "unknown"
					: p.PropertyType.TsType() + (nullable ? " | null" : "");
				return $"{name}{optional}: {propertyType};";
			});

		var body = string.Join(Environment.NewLine + "\t", props);

		return $$"""
export interface {{exportType.TsType()}} {
	{{body}}
}
""";
	}

	internal static Type? StripNullable(this Type? type)
	{
		if (type is null) return null;
		return Nullable.GetUnderlyingType(type) ?? type;
	}

	public static string TsType(this Type? type)
	{
		var stripped = StripNullable(type);

		// fallback to any for unknown types
		if (stripped == null)
			return "any";

		if (stripped.TsSimpleName(out var result))
			return result;

		if (stripped == typeof(byte[]))
			return "string";

		if (stripped.IsCollectionType(out result))
			return result;

		if (stripped.IsDictionaryType(out result))
			return result;

		if (stripped.IsCustomGenericType(out result))
			return result;

		// fallback for other system types
		if (stripped.IsSystemType())
			return "any";

		return stripped.Name.RemoveTypeArity();
	}

	private static bool IsAlwaysIgnored(PropertyInfo property)
	{
		var attribute = property.GetCustomAttribute<JsonIgnoreAttribute>();
		return attribute is not null && attribute.Condition == JsonIgnoreCondition.Always;
	}

	private static bool IsConditionallyIgnored(PropertyInfo property)
	{
		var condition = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition;
		return condition is JsonIgnoreCondition.WhenWritingDefault or JsonIgnoreCondition.WhenWritingNull;
	}

	static string GetTsDictionaryTypeName(this Type type)
	{
		var dictionary2Interface = type.GetInterface("System.Collections.Generic.IDictionary`2");
		if (dictionary2Interface != null || (type.FullName != null && type.FullName.StartsWith("System.Collections.Generic.IDictionary`2")))
		{
			var dictionaryType = dictionary2Interface ?? type;
			var keyType = dictionaryType.GetGenericArguments()[0];
			var valueType = dictionaryType.GetGenericArguments()[1];

			var keyTypeName = keyType.TsType();
			var valueTypeName = valueType.TsType();

			if (keyTypeName is not "number" and not "string")
			{
				throw new InvalidOperationException(
					$"Error when determining TypeScript type for C# type '{type.FullName}': " +
					"TypeScript dictionary key type must be either 'number' or 'string'");
			}

			return $"{{ [key: {keyTypeName}]: {valueTypeName}; }}";
		}

		// non-generic IDictionary
		if (type.GetInterface("System.Collections.IDictionary") != null ||
			(type.FullName != null && type.FullName.StartsWith("System.Collections.IDictionary")))
		{
			return "{ [key: string]: string; }";
		}

		return "any";
	}

	static Type? GetTsCollectionElementType(this Type type) => type.GetCollectionType();

	static bool IsCustomGenericType(this Type type, out string name, bool useGenericTypes = true)
	{
		name = "any";
		var value = type.GetTypeInfo().IsGenericType && !type.IsDictionaryType(out name) && !type.IsCollectionType(out name);
		if (!value) return value;

		// generally closed types, but can return T,U if useGenericTypes is false
		var genericArgs = useGenericTypes
			? type.GetGenericArguments()
			: type.GetGenericTypeDefinition().GetGenericArguments();

		var argumentNames = genericArgs
			.Select(t => t.IsGenericParameter ? t.Name : t.TsType())
			.ToArray();

		name = $"{type.Name.RemoveTypeArity()}<{string.Join(", ", argumentNames)}>";
		return value;
	}

	static bool IsCollectionType(this Type type, out string name)
	{
		var value = type.IsCollectionType();
		name = value ? (GetTsCollectionElementType(type)?.TsType() ?? "any") + "[]" : "any";
		return value;
	}

	static bool IsDictionaryType(this Type type, out string name)
	{
		var value = type.IsDictionaryType();
		name = !value ? "any" : type.GetTsDictionaryTypeName();
		return value;
	}

	public static bool IsSimpleType(this Type type)
	{
		return type.TsSimpleName(out _);
	}

	static bool TsSimpleName(this Type type, out string name)
	{
		name = "any";
		if (string.IsNullOrWhiteSpace(type.FullName))
		{
			return false;
		}

		switch (type.FullName)
		{
			case "System.Object":
				name = "unknown";
				return true;
			case "System.Boolean":
				name = "boolean";
				return true;
			case "System.Char":
			case "System.String":
			case "System.Guid":
			// Dates serialize to ISO 8601 strings — JSON.parse does not revive Date objects.
			case "System.DateTime":
			case "System.DateTimeOffset":
			case "System.DateOnly":
			case "System.TimeOnly":
			case "System.TimeSpan":
				name = "string";
				return true;
			case "System.SByte":
			case "System.Byte":
			case "System.Int16":
			case "System.UInt16":
			case "System.Int32":
			case "System.UInt32":
			case "System.Int64":
			case "System.UInt64":
			case "System.Single":
			case "System.Double":
			case "System.Decimal":
				name = "number";
				return true;
			default:
				return false;
		}
	}
}
