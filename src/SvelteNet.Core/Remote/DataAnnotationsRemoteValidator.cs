namespace SvelteNet.Remote;

using SvelteNet.TypeGen;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

/// <summary>
/// Default validator: DataAnnotations attributes on remote-function parameters
/// ([EmailAddress] string email) and on the properties of complex argument types
/// run automatically — the remote-function equivalent of MVC's model validation.
/// Attribute metadata is looked up once per method and cached; dispatch itself
/// stays generated code.
/// </summary>
public sealed class DataAnnotationsRemoteValidator : ISvelteRemoteValidator
{
	private static readonly ConcurrentDictionary<(Type Service, string Method), ParameterInfo[]> Parameters = new();

	public ValueTask ValidateAsync(RemoteValidationContext context)
	{
		var parameters = Parameters.GetOrAdd((context.ServiceType, context.Method.Name), static key =>
			key.Service
				.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.FirstOrDefault(m => m.Name == key.Method)?
				.GetParameters() ?? []);

		foreach (var parameter in parameters)
		{
			var name = parameter.Name!.ToCamelCase();
			// A field that already failed binding gets no second opinion.
			if (context.HasError(name) || !context.Arguments.TryGetValue(name, out var value)) continue;

			ValidateParameterAttributes(context, parameter, name, value);
			ValidateObjectGraph(context, value);
		}

		return ValueTask.CompletedTask;
	}

	private static void ValidateParameterAttributes(RemoteValidationContext context, ParameterInfo parameter, string name, object? value)
	{
		foreach (var attribute in parameter.GetCustomAttributes<ValidationAttribute>(inherit: false))
		{
			var validationContext = new ValidationContext(new object())
			{
				MemberName = parameter.Name,
				DisplayName = parameter.Name!
			};
			var result = attribute.GetValidationResult(value, validationContext);
			if (result is not null && result != ValidationResult.Success)
				context.AddError(name, result.ErrorMessage ?? $"Invalid value for '{name}'.");
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
