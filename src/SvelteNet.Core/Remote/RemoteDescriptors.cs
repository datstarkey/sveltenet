namespace SvelteNet.Remote;

using System.Collections.Concurrent;
using System.Reflection;
using SvelteNet.TypeGen;

public sealed record RemoteParameter(string Name, Type Type);

public sealed record RemoteMethodDescriptor(
	string Name,
	RemoteKind Kind,
	Func<object, RemoteArguments, ValueTask<object?>> Invoke,
	Type ReturnType,
	RemoteParameter[] Parameters);

public sealed record RemoteServiceDescriptor(
	string Name,
	Type ServiceType,
	RemoteMethodDescriptor[] Methods,
	bool IsGenerated);

/// <summary>
/// Registration point for remote-service descriptors. The SvelteNet.Generators
/// source generator emits a [ModuleInitializer] per [SvelteRemote] class that
/// registers a compiled descriptor here — no reflection at dispatch time. Classes
/// without a generated descriptor fall back to <see cref="FromReflection"/>.
/// </summary>
public static class SvelteRemoteDescriptors
{
	private static readonly ConcurrentDictionary<Type, RemoteServiceDescriptor> Registered = new();

	public static void Register(RemoteServiceDescriptor descriptor) => Registered[descriptor.ServiceType] = descriptor;

	/// <summary>Every descriptor registered so far (module initializers run at assembly load).</summary>
	public static IReadOnlyCollection<RemoteServiceDescriptor> All => Registered.Values.ToArray();

	public static RemoteServiceDescriptor For(Type serviceType) =>
		Registered.TryGetValue(serviceType, out var descriptor) ? descriptor : FromReflection(serviceType);

	/// <summary>Reflection fallback, used when the source generator is not referenced.</summary>
	public static RemoteServiceDescriptor FromReflection(Type serviceType)
	{
		var methods = serviceType
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.Select(m => (Method: m, Kind: KindOf(m)))
			.Where(x => x.Kind is not null)
			.Select(x => new RemoteMethodDescriptor(
				x.Method.Name,
				x.Kind!.Value,
				(service, args) => InvokeViaReflection(service, x.Method, args),
				UnwrapReturnType(x.Method.ReturnType),
				x.Method.GetParameters()
					.Where(p => p.ParameterType != typeof(CancellationToken))
					.Select(p => new RemoteParameter(p.Name!.ToCamelCase(), p.ParameterType))
					.ToArray()))
			.ToArray();

		return new RemoteServiceDescriptor(serviceType.Name, serviceType, methods, IsGenerated: false);
	}

	public static Type UnwrapReturnType(Type type)
	{
		if (type == typeof(Task) || type == typeof(ValueTask)) return typeof(void);
		if (type.IsGenericType &&
		    (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
			return type.GetGenericArguments()[0];
		return type;
	}

	private static RemoteKind? KindOf(MethodInfo method)
	{
		if (method.IsDefined(typeof(QueryAttribute), false)) return RemoteKind.Query;
		if (method.IsDefined(typeof(CommandAttribute), false)) return RemoteKind.Command;
		if (method.IsDefined(typeof(FormAttribute), false)) return RemoteKind.Form;
		return null;
	}

	private static async ValueTask<object?> InvokeViaReflection(object service, MethodInfo method, RemoteArguments args)
	{
		var parameters = method.GetParameters();
		var values = new object?[parameters.Length];
		for (var i = 0; i < parameters.Length; i++)
		{
			var p = parameters[i];
			values[i] = p.ParameterType == typeof(CancellationToken)
				? args.CancellationToken
				: args.Get(p.Name!.ToCamelCase(), p.ParameterType, p.HasDefaultValue, p.HasDefaultValue ? p.DefaultValue : null);
		}

		await args.ValidateBoundAsync();
		if (!args.CanInvoke) return null;

		try
		{
			return await UnwrapAsync(method.Invoke(service, values));
		}
		catch (System.Reflection.TargetInvocationException e) when (e.InnerException is not null)
		{
			throw e.InnerException;
		}
	}

	private static async Task<object?> UnwrapAsync(object? result)
	{
		switch (result)
		{
			case Task task:
			{
				await task;
				var type = task.GetType();
				if (type.IsGenericType && type.GetGenericArguments()[0].Name != "VoidTaskResult")
					return type.GetProperty(nameof(Task<object>.Result))!.GetValue(task);
				return null;
			}
			case ValueTask valueTask:
				await valueTask;
				return null;
		}

		var resultType = result?.GetType();
		if (resultType?.IsGenericType == true && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
		{
			var task = (Task)resultType.GetMethod(nameof(ValueTask<object>.AsTask))!.Invoke(result, null)!;
			await task;
			return task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
		}

		return result;
	}
}
