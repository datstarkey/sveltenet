namespace SvelteNet.Remote;

/// <summary>
/// Bring-your-own-validation hook for remote functions. Register any number of
/// implementations in DI; they run after argument binding and before the method
/// executes (for [Query], [Command], and [Form] alike, including validate-only
/// requests). Errors surface as the same RFC 9457 problem details the rest of
/// SvelteNet produces, so the frontend behaves identically no matter which
/// validator produced them.
/// </summary>
public interface ISvelteRemoteValidator
{
	ValueTask ValidateAsync(RemoteValidationContext context);
}

/// <summary>What a validator sees: the target method and its bound arguments.</summary>
public sealed class RemoteValidationContext
{
	private readonly RemoteArguments _args;

	public RemoteValidationContext(Type serviceType, RemoteMethodDescriptor method, RemoteArguments args)
	{
		ServiceType = serviceType;
		Method = method;
		_args = args;
	}

	public Type ServiceType { get; }
	public RemoteMethodDescriptor Method { get; }

	/// <summary>Bound values keyed by camelCase parameter name.</summary>
	public IReadOnlyDictionary<string, object?> Arguments => _args.Bound;

	public CancellationToken CancellationToken => _args.CancellationToken;

	/// <summary>True when binding (or an earlier validator) already reported this field.</summary>
	public bool HasError(string field) => _args.HasError(field);

	/// <summary>Reports a validation error; an empty field name means form-level.</summary>
	public void AddError(string field, string message) => _args.AddError(field, message);
}
