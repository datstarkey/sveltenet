namespace SvelteNet.AspNetCore;

using System.Reflection;

internal static class TypeDiscovery
{
	public static List<Type> FindTypes(Func<Type, bool> predicate)
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic)
			.SelectMany(GetLoadableTypes)
			.Where(predicate)
			.ToList();
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException e)
		{
			return e.Types.Where(t => t != null)!;
		}
	}
}
