namespace Svelte.Net.Core.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class TypeExtensions
{
	public static List<Type> GetAllTypes(this IEnumerable<Type> types)
	{
		return types.SelectMany(s => s.GetAllTypes())
			.Distinct()
			.ToList();
	}

	public static bool IsSystemType(this Type type)
	{
		return type.FullName?.StartsWith("System.") ?? false;
	}

	
	
	static bool IsValidType(Type type, out Type? stripped)
	{
		stripped = type.StripNullable()
			.Flatten();
		return stripped != null &&
		       (!stripped.IsGenericType || stripped.IsCustomGenericType()) &&
		       !stripped.IsSimpleType() 
		       && !stripped.IsSystemType() 
		       && stripped is not { IsEnum: false, IsClass: false } 
		       && stripped is { IsPublic: true, IsPrimitive: false };

	}

	static List<Type> GetGenericTypes(this Type type)
	{
		var list = new List<Type>();
		if (!type.IsGenericType) return list;
		var args = type.GetGenericArguments();
		foreach (var a in args)
		{
			list.Add(a);
			list.AddRange(a.GetGenericTypes());
		}
		if(type.IsCustomGenericType())
			list.Add(type);
		return list;
	}

	static List<Type> GetPropTypes(this Type type)
	{
		return type.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead)
			.Select(p => p.PropertyType)
			.Distinct()
			.ToList();
	}

	public static List<Type> GetAllTypes(this Type type)
	{

		var list = new List<Type>();
		void AddToList(Type t)
		{
			if(!IsValidType(t,out var result) || result == null ||  list.Contains(result)) return;
			list.Add(result);
		}
		AddToList(type);
		var index = 0;
		while (index<list.Count)
		{
			var t = list[index];
			
			if (t.IsGenericType)
			{
				foreach (var a in GetGenericTypes(t))
					AddToList(a);
			}
			
			var props = t.GetPropTypes();
			foreach (var prop in props)
			{
				AddToList(prop);
			}
			index++;
		}
		return list;
		
	}

	public static bool IsCustomGenericType(this Type type)
	{
		return type.GetTypeInfo().IsGenericType && !type.IsDictionaryType() && !type.IsCollectionType();
	}

	public static bool IsCollectionType(this Type type)
	{
		return type.FullName != "System.String"// not a string
		       && !type.IsDictionaryType()// not a dictionary
		       && (type.GetInterface("IEnumerable") != null || (type.FullName != null && type.FullName.StartsWith("System.Collections.IEnumerable")));// implements IEnumerable or is IEnumerable
	}

	public static bool IsDictionaryType(this Type type)
	{
		return type.GetInterface("System.Collections.Generic.IDictionary`2") != null
		       || (type.FullName != null && type.FullName.StartsWith("System.Collections.Generic.IDictionary`2"))
		       || type.GetInterface("System.Collections.IDictionary") != null
		       || (type.FullName != null && type.FullName.StartsWith("System.Collections.IDictionary"));

	}

	public static Type? Flatten(this Type? type)
	{
		if (type?.IsSimpleType() == true) return type;
		while (type != null && type.IsCollectionType())
		{
			type = type.GetCollectionType();
		}
		return type;
	}

	public static Type? GetCollectionType(this Type? type)
	{
		if (type == null) return null;
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

		var enumerableInterface = type.GetInterface("IEnumerable`1");
		if (enumerableInterface != null) return enumerableInterface.GetGenericArguments()[0];

		var iEnumerable = type.GetInterface("IEnumerable");
		return iEnumerable != null ? typeof(object) : null;

	}

	public static Type? GetBaseType(Type type)
	{
		var baseType = type.GetTypeInfo().BaseType;
		if (baseType == null || baseType == typeof(object)) return null;
		return baseType;
	}

	public static IEnumerable<Type> GetInterfaces(Type type)
	{
		IEnumerable<Type> baseTypes = type.GetTypeInfo().ImplementedInterfaces;
		return baseTypes;
	}
}
