namespace SvelteNet.Core.Tests;

using SvelteNet.Core.Tests.Fixtures;
using SvelteNet.TypeGen;

public class TsTypeTests
{
	[Theory]
	[InlineData(typeof(int), "number")]
	[InlineData(typeof(int?), "number")]
	[InlineData(typeof(decimal), "number")]
	[InlineData(typeof(string), "string")]
	[InlineData(typeof(bool), "boolean")]
	[InlineData(typeof(Guid), "string")]
	[InlineData(typeof(DateTime), "string")]
	[InlineData(typeof(DateTimeOffset), "string")]
	[InlineData(typeof(DateOnly), "string")]
	[InlineData(typeof(TimeSpan), "string")]
	[InlineData(typeof(object), "unknown")]
	public void Maps_simple_types(Type type, string expected)
	{
		Assert.Equal(expected, type.TsType());
	}

	[Theory]
	[InlineData(typeof(List<Order>), "Order[]")]
	[InlineData(typeof(Order[]), "Order[]")]
	[InlineData(typeof(IReadOnlyList<Order>), "Order[]")]
	[InlineData(typeof(IEnumerable<int>), "number[]")]
	public void Maps_collections(Type type, string expected)
	{
		Assert.Equal(expected, type.TsType());
	}

	[Theory]
	[InlineData(typeof(Dictionary<string, int>), "{ [key: string]: number; }")]
	[InlineData(typeof(Dictionary<int, string>), "{ [key: number]: string; }")]
	[InlineData(typeof(Dictionary<Guid, Order>), "{ [key: string]: Order; }")]
	public void Maps_dictionaries(Type type, string expected)
	{
		Assert.Equal(expected, type.TsType());
	}

	[Fact]
	public void Maps_generics_with_closed_arguments()
	{
		Assert.Equal("Paged<Order>", typeof(Paged<Order>).TsType());
	}
}

public class TypescriptInterfaceTests
{
	[Fact]
	public void Enums_are_camel_cased_string_unions_matching_the_serializer()
	{
		var ts = typeof(OrderStatus).GetTypescriptInterface();
		Assert.Equal("export type OrderStatus = 'pendingApproval' | 'shipped';", ts);
	}

	[Fact]
	public void Nullable_properties_get_null_unions()
	{
		var ts = typeof(Customer).GetTypescriptInterface();
		Assert.Contains("name: string;", ts);
		Assert.Contains("address: Address | null;", ts);
	}

	[Fact]
	public void Generic_definitions_use_type_parameters()
	{
		var ts = typeof(Paged<Order>).GetTypescriptInterface(useGenericTypes: false);
		Assert.Contains("export interface Paged<T>", ts);
		Assert.Contains("items: T[];", ts);
		Assert.Contains("total: number;", ts);
	}

	[Fact]
	public void Interface_includes_all_readable_properties()
	{
		var ts = typeof(Order).GetTypescriptInterface();
		Assert.Contains("export interface Order", ts);
		Assert.Contains("id: string;", ts);
		Assert.Contains("createdAt: string;", ts);
		Assert.Contains("status: OrderStatus;", ts);
		Assert.Contains("lines: OrderLine[];", ts);
		Assert.Contains("customers: { [key: string]: Customer; };", ts);
	}
}

public class GetAllTypesTests
{
	[Fact]
	public void Discovers_types_transitively()
	{
		var types = new[] { typeof(Order) }.GetAllTypes();

		Assert.Contains(typeof(Order), types);
		Assert.Contains(typeof(OrderLine), types);
		Assert.Contains(typeof(OrderStatus), types);
	}

	[Fact]
	public void Discovers_dictionary_value_types()
	{
		var types = new[] { typeof(Order) }.GetAllTypes();

		Assert.Contains(typeof(Customer), types);
		Assert.Contains(typeof(Address), types);
	}

	[Fact]
	public void Discovers_generic_arguments()
	{
		var types = new[] { typeof(Paged<Customer>) }.GetAllTypes();

		Assert.Contains(typeof(Paged<Customer>), types);
		Assert.Contains(typeof(Customer), types);
		Assert.Contains(typeof(Address), types);
	}

	[Fact]
	public void Excludes_simple_and_system_types()
	{
		var types = new[] { typeof(Order) }.GetAllTypes();

		Assert.DoesNotContain(typeof(string), types);
		Assert.DoesNotContain(typeof(Guid), types);
		Assert.DoesNotContain(typeof(DateTime), types);
	}
}
