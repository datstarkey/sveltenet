namespace Svelte.Net.Core.Extensions;

using Jint.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class TypeScriptExtensions
{


	public static string GetTypescriptInterface(this Type type,bool useGenericTypes = true)
	{
		
		var exportType = type.StripNullable() ?? type;


		if (exportType.IsEnum)
		{
			return $@"
export type {exportType.TsType()} = {string.Join(" | ", Enum.GetNames(exportType).Select(s => $"'{s}'"))}";
		}
		
		
		exportType = useGenericTypes || !exportType.IsGenericType ? exportType : exportType.GetGenericTypeDefinition();

		var props = exportType
			.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead)
			.Select(p => $"{p.Name.ToCamelCase()} : {p.PropertyType.TsType()}");

		var str = string.Join(Environment.NewLine + "    ", props);
		

		return $@"
export type {exportType.TsType()} = {{
	{str}
}}";
	}
	
	
	private static readonly HashSet<Type> IgnoredGenerics = new HashSet<Type>
	{
		typeof(ValueType)
	};

	internal static Type? StripNullable(this Type? type)
	{
		var  nullableUnderlyingType = Nullable.GetUnderlyingType(type);
		return nullableUnderlyingType ?? type;
	}

	public static string TsType(this Type? type)
	{
		var stripped = StripNullable(type);
		var result = "any";

		//fallback to any for unknown types
		if (stripped == null)
			return result;


		if (stripped.TsSimpleName(out result))
			return result;

		if (stripped.IsCollectionType(out result))
			return result;

		if (stripped.IsDictionaryType(out result))
			return result;

		if (stripped.IsCustomGenericType(out result))
			return result;

		//fallback for system types
		if (stripped.FullName?.StartsWith("System.") == true)
		{
			return "any";
		}
		
		// if(stripped.IsEnum)
		// 	return Enum.GetNames(stripped).Select(s=>$"'{s}'").Aggregate((a,b)=>a+" | "+b);

		
		return stripped.Name.RemoveTypeArity();
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
				throw new Exception($"Error when determining TypeScript type for C# type '{type.FullName}':" +
				                    "TypeScript dictionary key type must be either 'number' or 'string'");
			}

			return $"{{ [key: {keyTypeName}]: {valueTypeName}; }}";
		}

		// handle IDictionary
		if (type.GetInterface("System.Collections.IDictionary") != null ||
		    (type.FullName != null && type.FullName.StartsWith("System.Collections.IDictionary")))
		{
			return $"{{ [key: string]: string; }}";
		}

		return "any";
	}

	static Type? GetTsCollectionElementType(this Type type)
	{
		// handle array types
		var elementType = type.GetElementType();
		if (elementType != null)
		{
			return elementType;
		}

		switch (type.Name)
		{
			// handle IEnumerable<>
			case "IEnumerable`1":
				return type.GetGenericArguments()[0];
			// handle IEnumerable
			case "IEnumerable":
				return typeof(object);
		}

		// handle types implementing IEnumerable or IEnumerable<>

		var ienumerable1Interface = type.GetInterface("IEnumerable`1");
		if (ienumerable1Interface != null) return ienumerable1Interface.GetGenericArguments()[0];
            
		var ienumerableInterface = type.GetInterface("IEnumerable");
		return ienumerableInterface != null ? typeof(object) : null;

	}
	
	static bool IsIgnoredType(this Type type)
	{
		return IgnoredGenerics.Contains(type);
	}
	
	static string RemoveTypeArity(this string value)
	{
		return value.Split('`')[0];
	}
	
	static bool IsCustomGenericType(this Type type, out string name, bool useGenericTypes = true)
	{
		name = "any";
		var value =  type.GetTypeInfo().IsGenericType && !type.IsDictionaryType(out name) && !type.IsCollectionType(out name);

		if (!value) return value;

		
		//generally return types, but can return T,U if useGenericTypes is false
		var genericArgs = useGenericTypes?  
			type.GetGenericArguments() : 
			type.GetGenericTypeDefinition().GetGenericArguments();


		
		var argumentNames = genericArgs
			.Select(t => t.IsGenericParameter ? t.Name : t.TsType())
			.ToArray();

		var typeName = type.Name.RemoveTypeArity();
		var args = string.Join(", ", argumentNames);
		name = $"{typeName}<{args}>";
		return value;
	}

	
	static bool IsCollectionType(this Type type, out string name)
	{
		var value = type.IsCollectionType();
		name = value ? (GetTsCollectionElementType(type)?.TsType() ?? "any") + "[]" : "any";
		return value;
	}

	static bool IsDictionaryType(this Type type,  out string name)
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
		};
		
		switch (type.FullName)
		{
			case "System.Object":
				name = "Object";
				return true;
			case "System.Boolean":
				name = "boolean";
				return true;
			case "System.Char":
			case "System.String":
			case "System.Guid":
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
			case "System.DateTime":
			case "System.DateTimeOffset":
				name = "Date";
				return true;
			default:
				return false;
		}
	}
}
