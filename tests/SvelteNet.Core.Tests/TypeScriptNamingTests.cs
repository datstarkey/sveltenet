namespace SvelteNet.Core.Tests;

using SvelteNet.TypeGen;

public class TypeScriptNamingTests
{
	[Fact]
	public void Unique_short_names_stay_short()
	{
		var resolved = TypeScriptNaming.Resolve(["App_Orders_Todo", "App_Weather_Forecast"]);

		Assert.Equal("Todo", resolved["App_Orders_Todo"]);
		Assert.Equal("Forecast", resolved["App_Weather_Forecast"]);
	}

	/// <summary>
	/// Two CLR types called Item used to produce two `interface Item` declarations in one
	/// ambient block (reflection) or a hard build failure with no way out (generated).
	/// </summary>
	[Fact]
	public void Colliding_short_names_are_qualified_with_their_namespace()
	{
		var resolved = TypeScriptNaming.Resolve(["App_Orders_Item", "App_Catalog_Item"]);

		Assert.Equal("OrdersItem", resolved["App_Orders_Item"]);
		Assert.Equal("CatalogItem", resolved["App_Catalog_Item"]);
		Assert.Equal(2, resolved.Values.Distinct().Count());
	}

	/// <summary>
	/// Generated interfaces are ambient globals, so a model called File would shadow the
	/// DOM's File across the whole project.
	/// </summary>
	[Theory]
	[InlineData("App_Storage_File", "StorageFile")]
	[InlineData("App_Audit_Event", "AuditEvent")]
	[InlineData("App_Http_Request", "HttpRequest")]
	[InlineData("App_Api_Response", "ApiResponse")]
	public void Names_that_shadow_dom_globals_are_qualified(string placeholder, string expected)
	{
		Assert.Equal(expected, TypeScriptNaming.Resolve([placeholder])[placeholder]);
	}

	[Fact]
	public void Explicit_names_win_and_are_reserved_first()
	{
		var resolved = TypeScriptNaming.Resolve(
			["App_Orders_Item", "App_Catalog_Item"],
			new Dictionary<string, string> { ["App_Catalog_Item"] = "Item" });

		Assert.Equal("Item", resolved["App_Catalog_Item"]);
		// The automatic name must not steal the explicit one.
		Assert.NotEqual("Item", resolved["App_Orders_Item"]);
	}

	[Fact]
	public void Deeper_qualification_resolves_a_second_level_collision()
	{
		var resolved = TypeScriptNaming.Resolve(["A_Orders_Item", "B_Orders_Item"]);

		Assert.Equal(2, resolved.Values.Distinct().Count());
		Assert.Contains("Orders", resolved["A_Orders_Item"]);
	}

	[Fact]
	public void Substitution_replaces_whole_identifiers_only()
	{
		var resolved = new Dictionary<string, string> { ["App_Item"] = "Item" };

		var result = TypeScriptNaming.Substitute("interface App_Item { child: App_Item; other: App_ItemList; }", resolved);

		Assert.Equal("interface Item { child: Item; other: App_ItemList; }", result);
	}

	[Fact]
	public void Substitution_handles_generics_and_arrays()
	{
		var resolved = new Dictionary<string, string>
		{
			["App_Paged"] = "Paged",
			["App_Orders_Item"] = "OrdersItem"
		};

		var result = TypeScriptNaming.Substitute("items: App_Paged<App_Orders_Item>[] | null;", resolved);

		Assert.Equal("items: Paged<OrdersItem>[] | null;", result);
	}

	[Fact]
	public void Substitution_leaves_unknown_identifiers_alone()
	{
		var result = TypeScriptNaming.Substitute("x: string; y: number;", new Dictionary<string, string> { ["Foo"] = "Bar" });

		Assert.Equal("x: string; y: number;", result);
	}

	[Fact]
	public void Resolution_is_deterministic_regardless_of_input_order()
	{
		var forward = TypeScriptNaming.Resolve(["App_Orders_Item", "App_Catalog_Item", "App_X_Todo"]);
		var reverse = TypeScriptNaming.Resolve(["App_X_Todo", "App_Catalog_Item", "App_Orders_Item"]);

		Assert.Equal(forward.OrderBy(kv => kv.Key), reverse.OrderBy(kv => kv.Key));
	}
}
