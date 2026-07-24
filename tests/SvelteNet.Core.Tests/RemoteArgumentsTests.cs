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
}
