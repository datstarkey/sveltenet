namespace SvelteNet.AspNetCore;

using System.Reflection;

internal static class TypeDiscovery
{
	/// <summary>
	/// Finds matching types in the given assemblies, or in every loaded assembly
	/// when none are given. Scoping matters when multiple SvelteNet apps share a
	/// process (e.g. WebApplicationFactory test hosts) — an unscoped scan would
	/// register one app's services into another's container.
	/// </summary>
	public static List<Type> FindTypes(IReadOnlyList<Assembly>? assemblies, Func<Type, bool> predicate)
	{
		var scope = assemblies is { Count: > 0 }
			? (IEnumerable<Assembly>)assemblies
			: AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic);

		return scope
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
