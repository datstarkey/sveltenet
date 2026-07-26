namespace SvelteNet.Core.Tests;

public class SvelteOptionsTests
{
	/// <summary>
	/// ClientOutput and ClientPublicPath describe the same directory from two sides. They
	/// used to be independent defaults, so changing one silently broke asset URLs.
	/// </summary>
	[Theory]
	[InlineData("wwwroot/client", "/client")]
	[InlineData("wwwroot/assets/svelte", "/assets/svelte")]
	[InlineData("wwwroot\\client", "/client")]
	[InlineData("wwwroot/", "/")]
	public void Client_public_path_follows_client_output(string output, string expected)
	{
		var options = new SvelteOptions { ClientOutput = output };

		Assert.Equal(expected, options.ClientPublicPath);
	}

	[Fact]
	public void An_explicit_public_path_wins()
	{
		var options = new SvelteOptions
		{
			ClientOutput = "wwwroot/client",
			ClientPublicPath = "https://cdn.example.test/app"
		};

		Assert.Equal("https://cdn.example.test/app", options.ClientPublicPath);
	}

	[Fact]
	public void A_custom_web_root_is_stripped()
	{
		var options = new SvelteOptions { WebRoot = "public", ClientOutput = "public/client" };

		Assert.Equal("/client", options.ClientPublicPath);
	}

	/// <summary>Output outside the web root is left alone rather than mangled.</summary>
	[Fact]
	public void Output_outside_the_web_root_keeps_its_path()
	{
		var options = new SvelteOptions { ClientOutput = "static/client" };

		Assert.Equal("/static/client", options.ClientPublicPath);
	}
}
