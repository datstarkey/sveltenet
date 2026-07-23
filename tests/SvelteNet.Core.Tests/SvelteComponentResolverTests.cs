namespace SvelteNet.Core.Tests;

[SvelteComponent("Widgets/Special")]
public class ExplicitPathViewModel;

[SvelteComponent]
public class CartViewModel;

[SvelteComponent]
public class CartModel;

[SvelteComponent]
public class CartDto;

[SvelteComponent]
public class Cart;

public class UnattributedModel;

public class SvelteComponentResolverTests
{
	[Fact]
	public void Explicit_paths_win()
	{
		Assert.Equal("Widgets/Special", SvelteComponentResolver.Resolve(typeof(ExplicitPathViewModel)));
	}

	[Theory]
	[InlineData(typeof(CartViewModel))]
	[InlineData(typeof(CartModel))]
	[InlineData(typeof(CartDto))]
	[InlineData(typeof(Cart))]
	public void Convention_trims_model_suffixes(Type type)
	{
		Assert.Equal("Components/Cart", SvelteComponentResolver.Resolve(type));
	}

	[Fact]
	public void Missing_attribute_throws_with_guidance()
	{
		var ex = Assert.Throws<InvalidOperationException>(() => SvelteComponentResolver.Resolve(typeof(UnattributedModel)));

		Assert.Contains("SvelteComponent", ex.Message);
		Assert.Contains("UnattributedModel", ex.Message);
	}
}
