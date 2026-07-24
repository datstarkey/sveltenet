namespace SvelteNet.FluentValidation;

using global::FluentValidation;
using SvelteNet.Remote;
using SvelteNet.TypeGen;

/// <summary>
/// Runs FluentValidation over remote-function arguments: for each bound value an
/// IValidator&lt;T&gt; is resolved from DI (when one is registered) and its failures
/// flow into the same RFC 9457 problem details every other validation source uses —
/// the frontend cannot tell the difference. Resolved per request, so validators can
/// be scoped and take their own dependencies.
/// </summary>
public sealed class FluentValidationRemoteValidator(IServiceProvider services) : ISvelteRemoteValidator
{
	public async ValueTask ValidateAsync(RemoteValidationContext context)
	{
		foreach (var (name, value) in context.Arguments)
		{
			if (value is null || context.HasError(name)) continue;

			var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
			if (services.GetService(validatorType) is not IValidator validator) continue;

			var result = await validator.ValidateAsync(
				new ValidationContext<object>(value), context.CancellationToken);

			foreach (var failure in result.Errors)
				context.AddError(CamelPath(failure.PropertyName), failure.ErrorMessage);
		}
	}

	/// <summary>"Address.City" → "address.city", matching the camelCase JSON contract.</summary>
	private static string CamelPath(string propertyPath) =>
		propertyPath.Length == 0
			? string.Empty
			: string.Join('.', propertyPath.Split('.').Select(StringExtensions.ToCamelCase));
}
