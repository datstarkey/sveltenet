namespace SvelteNet.FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SvelteNet.Remote;

public static class SvelteNetFluentValidationExtensions
{
	/// <summary>
	/// Plugs FluentValidation into SvelteNet's remote-function validation pipeline.
	/// Register your IValidator&lt;T&gt;s as usual (individually, or with
	/// FluentValidation.DependencyInjectionExtensions' AddValidatorsFromAssembly) —
	/// they run automatically before the remote method executes.
	/// </summary>
	public static IServiceCollection AddSvelteNetFluentValidation(this IServiceCollection services)
	{
		services.TryAddEnumerable(ServiceDescriptor.Scoped<ISvelteRemoteValidator, FluentValidationRemoteValidator>());
		return services;
	}
}
