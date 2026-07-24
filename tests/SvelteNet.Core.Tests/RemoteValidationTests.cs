namespace SvelteNet.Core.Tests;

using SvelteNet.Remote;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

/// <summary>
/// The BYOV validation pipeline: validators run between binding and invocation
/// (here through the reflection dispatcher; the generated dispatchers emit the
/// same ValidateBoundAsync call), and DataAnnotations work out of the box.
/// </summary>
public class RemoteValidationTests
{
	public class Signup
	{
		[Required] public string? Name { get; set; }
		[Range(18, 120)] public int Age { get; set; }
	}

	[SvelteRemote]
	public class Api
	{
		public string? LastEmail;

		[Command]
		public string Invite([EmailAddress] string email)
		{
			LastEmail = email;
			return email;
		}

		[Command]
		public string Register(Signup signup) => signup.Name!;
	}

	private static RemoteMethodDescriptor Method(string name) =>
		SvelteRemoteDescriptors.FromReflection(typeof(Api)).Methods.Single(m => m.Name == name);

	[Fact]
	public async Task Parameter_attributes_block_invocation_and_report_the_field()
	{
		var api = new Api();
		var args = new RemoteArguments
		{
			Json = JsonSerializer.SerializeToElement(new { email = "nope" }, SvelteJson.Options),
			Validation = MakeValidation("Invite")
		};

		var result = await Method("Invite").Invoke(api, args);

		Assert.Null(result);
		Assert.Null(api.LastEmail);
		var messages = Assert.Contains("email", args.Errors!);
		Assert.NotEmpty(messages);
	}

	[Fact]
	public async Task Valid_arguments_pass_through_untouched()
	{
		var api = new Api();
		var args = new RemoteArguments
		{
			Json = JsonSerializer.SerializeToElement(new { email = "a@b.com" }, SvelteJson.Options),
			Validation = MakeValidation("Invite")
		};

		var result = await Method("Invite").Invoke(api, args);

		Assert.Equal("a@b.com", result);
		Assert.Equal("a@b.com", api.LastEmail);
		Assert.Null(args.Errors);
	}

	[Fact]
	public async Task Complex_argument_types_validate_their_property_annotations()
	{
		var args = new RemoteArguments
		{
			Json = JsonSerializer.SerializeToElement(new { signup = new { age = 12 } }, SvelteJson.Options),
			Validation = MakeValidation("Register")
		};

		var result = await Method("Register").Invoke(new Api(), args);

		Assert.Null(result);
		Assert.Contains("name", args.Errors!);
		Assert.Contains("age", args.Errors!);
	}

	[Fact]
	public async Task Custom_validators_compose_with_the_pipeline()
	{
		var custom = new DelegateValidator(ctx =>
		{
			if (ctx.Arguments["email"] is string email && email.EndsWith("@blocked.com"))
				ctx.AddError("email", "Domain is blocked.");
		});
		var args = new RemoteArguments
		{
			Json = JsonSerializer.SerializeToElement(new { email = "a@blocked.com" }, SvelteJson.Options),
			Validation = MakeValidation("Invite", custom)
		};

		await Method("Invite").Invoke(new Api(), args);

		Assert.Equal("Domain is blocked.", Assert.Contains("email", args.Errors!).Single());
	}

	private static Func<RemoteArguments, ValueTask> MakeValidation(string method, params ISvelteRemoteValidator[] extra)
	{
		ISvelteRemoteValidator[] validators = [new DataAnnotationsRemoteValidator(), .. extra];
		var descriptor = Method(method);
		return async args =>
		{
			var context = new RemoteValidationContext(typeof(Api), descriptor, args);
			foreach (var validator in validators) await validator.ValidateAsync(context);
		};
	}

	private sealed class DelegateValidator(Action<RemoteValidationContext> validate) : ISvelteRemoteValidator
	{
		public ValueTask ValidateAsync(RemoteValidationContext context)
		{
			validate(context);
			return ValueTask.CompletedTask;
		}
	}
}
