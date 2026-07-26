namespace SvelteNet.Core.Tests;

using SvelteNet.Remote;

public class RemoteArgumentsTests
{
	[Fact]
	public void Repeated_form_values_bind_to_collections()
	{
		var args = new RemoteArguments
		{
			Form = new Dictionary<string, IReadOnlyList<string>>
			{
				["ids"] = ["1", "2", "3"]
			}
		};

		var ids = args.Get<int[]>("ids");

		Assert.NotNull(ids);
		Assert.Equal([1, 2, 3], ids);
		Assert.Null(args.Errors);
	}

	[Fact]
	public void Missing_checkbox_values_bind_to_false()
	{
		var args = new RemoteArguments
		{
			Form = new Dictionary<string, IReadOnlyList<string>>()
		};

		Assert.False(args.Get<bool>("enabled"));
		Assert.Null(args.Errors);
	}

	[Fact]
	public void Invalid_boolean_values_report_binding_errors()
	{
		var args = new RemoteArguments
		{
			Form = new Dictionary<string, IReadOnlyList<string>>
			{
				["enabled"] = ["definitely"]
			}
		};

		Assert.False(args.Get<bool>("enabled"));
		Assert.Contains("enabled", args.Errors!);
	}

	private static RemoteArguments Form(string field, params string[] values) =>
		new() { Form = new Dictionary<string, IReadOnlyList<string>> { [field] = values } };

	/// <summary>
	/// Form fields accept the same spelling JSON does (camelCase, per
	/// JsonStringEnumConverter), so a field is not spelled one way over one transport and
	/// another way over the other. The CLR member name keeps working for hand-written forms.
	/// </summary>
	[Theory]
	[InlineData("pendingApproval")]
	[InlineData("PendingApproval")]
	[InlineData("pendingapproval")]
	public void Enums_bind_from_both_json_and_clr_spellings(string value)
	{
		var args = Form("status", value);

		Assert.Equal(Fixtures.OrderStatus.PendingApproval, args.Get<Fixtures.OrderStatus>("status"));
		Assert.Null(args.Errors);
	}

	/// <summary>
	/// Enum.Parse accepts any numeric string, so "999" used to bind as (OrderStatus)999 and
	/// reach the handler as a value that does not exist.
	/// </summary>
	[Theory]
	[InlineData("999")]
	[InlineData("-1")]
	[InlineData("notAStatus")]
	public void Undefined_enum_values_are_rejected(string value)
	{
		var args = Form("status", value);

		args.Get<Fixtures.OrderStatus>("status");

		Assert.Contains("status", args.Errors!);
	}

	/// <summary>
	/// Scalars used to be wrapped in quotes by string concatenation, so any field
	/// containing a quote or backslash produced malformed JSON.
	/// </summary>
	[Theory]
	[InlineData("O\"Brien")]
	[InlineData("back\\slash")]
	[InlineData("line\nbreak")]
	public void Quotes_and_backslashes_survive_scalar_binding(string value)
	{
		var args = Form("name", value);

		Assert.Equal(value, args.Get<string>("name"));
		Assert.Null(args.Errors);
	}

	[Fact]
	public void Guids_still_bind_from_their_unquoted_form()
	{
		var id = Guid.NewGuid();

		var args = Form("id", id.ToString());

		Assert.Equal(id, args.Get<Guid>("id"));
		Assert.Null(args.Errors);
	}
}
