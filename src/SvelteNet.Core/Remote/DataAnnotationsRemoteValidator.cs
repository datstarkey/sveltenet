namespace SvelteNet.Remote;

using System.ComponentModel.DataAnnotations;
using SvelteNet.TypeGen;

/// <summary>
/// Default validator: DataAnnotations attributes on remote-function parameters
/// ([EmailAddress] string email) and on the properties of complex argument types
/// run automatically — the remote-function equivalent of MVC's model validation.
/// Attribute metadata is looked up once per method and cached; dispatch itself
/// stays generated code.
/// </summary>
public sealed class DataAnnotationsRemoteValidator : ISvelteRemoteValidator
{
	public ValueTask ValidateAsync(RemoteValidationContext context)
	{
		foreach (var parameter in context.Method.Parameters)
		{
			var name = parameter.Name;
			// A field that already failed binding gets no second opinion.
			if (context.HasError(name) || !context.Arguments.TryGetValue(name, out var value)) continue;

			ValidateParameterAttributes(context, parameter, value);
			ValidateObjectGraph(context, value);
		}

		return ValueTask.CompletedTask;
	}

	private static void ValidateParameterAttributes(RemoteValidationContext context, RemoteParameter parameter, object? value)
	{
		foreach (var attribute in parameter.ValidationAttributes ?? [])
		{
			var validationContext = new ValidationContext(new object())
			{
				MemberName = parameter.Name,
				DisplayName = parameter.Name
			};
			var result = attribute.GetValidationResult(value, validationContext);
			if (result is not null && result != ValidationResult.Success)
				context.AddError(parameter.Name, result.ErrorMessage ?? $"Invalid value for '{parameter.Name}'.");
		}
	}

	private static void ValidateObjectGraph(RemoteValidationContext context, object? value)
	{
		if (value is null || value is string || value.GetType().IsValueType) return;

		var results = new List<ValidationResult>();
		if (Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true)) return;

		foreach (var result in results)
		{
			var members = result.MemberNames.Any() ? result.MemberNames : [""];
			foreach (var member in members)
				context.AddError(member.ToCamelCase(), result.ErrorMessage ?? "Invalid value.");
		}
	}
}
