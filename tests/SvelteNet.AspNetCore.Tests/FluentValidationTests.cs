namespace SvelteNet.AspNetCore.Tests;

using System.Text.Json;
using global::FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SvelteNet.FluentValidation;
using SvelteNet.Remote;

/// <summary>
/// The FluentValidation adapter: IValidator&lt;T&gt;s resolved from DI run over bound
/// arguments through the same pipeline as every other validator.
/// </summary>
public class FluentValidationTests
{
	public record Feedback(string Message, int Rating);

	public class FeedbackValidator : AbstractValidator<Feedback>
	{
		public FeedbackValidator()
		{
			RuleFor(f => f.Message).NotEmpty();
			RuleFor(f => f.Rating).InclusiveBetween(1, 5).WithMessage("Out of range.");
		}
	}

	[SvelteRemote]
	public class Api
	{
		public bool Ran;

		[Command]
		public string Submit(Feedback feedback)
		{
			Ran = true;
			return "ok";
		}
	}

	private static RemoteArguments Args(object payload, IServiceProvider services)
	{
		var descriptor = SvelteRemoteDescriptors.FromReflection(typeof(Api)).Methods.Single();
		return new RemoteArguments
		{
			Json = JsonSerializer.SerializeToElement(payload, SvelteJson.Options),
			Validation = SvelteNet.AspNetCore.Remote.SvelteRemoteEndpoints.BuildValidation(services, typeof(Api), descriptor)
		};
	}

	private static IServiceProvider Services() => new ServiceCollection()
		.AddSvelteNetFluentValidation()
		.AddScoped<IValidator<Feedback>, FeedbackValidator>()
		.BuildServiceProvider();

	[Fact]
	public async Task Registered_validators_block_invocation_with_camel_cased_property_errors()
	{
		var api = new Api();
		var args = Args(new { feedback = new { message = "", rating = 9 } }, Services());

		var result = await SvelteRemoteDescriptors.FromReflection(typeof(Api)).Methods.Single().Invoke(api, args);

		Assert.Null(result);
		Assert.False(api.Ran);
		Assert.Contains("message", args.Errors!);
		Assert.Equal("Out of range.", Assert.Contains("rating", args.Errors!).Single());
	}

	[Fact]
	public async Task Valid_arguments_run_the_method()
	{
		var api = new Api();
		var args = Args(new { feedback = new { message = "nice", rating = 4 } }, Services());

		var result = await SvelteRemoteDescriptors.FromReflection(typeof(Api)).Methods.Single().Invoke(api, args);

		Assert.Equal("ok", result);
		Assert.True(api.Ran);
		Assert.Null(args.Errors);
	}

	[Fact]
	public async Task Arguments_without_a_registered_validator_pass_through()
	{
		var services = new ServiceCollection().AddSvelteNetFluentValidation().BuildServiceProvider();
		var api = new Api();
		var args = Args(new { feedback = new { message = "", rating = 9 } }, services);

		var result = await SvelteRemoteDescriptors.FromReflection(typeof(Api)).Methods.Single().Invoke(api, args);

		Assert.Equal("ok", result);
		Assert.Null(args.Errors);
	}
}
