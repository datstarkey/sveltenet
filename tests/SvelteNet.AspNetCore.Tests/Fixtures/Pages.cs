// The scaffolder mirrors the Razor Pages folder structure by splitting the namespace
// at ".Pages", so these fixtures live in deliberately conventional namespaces.

namespace SvelteNet.AspNetCore.Tests.Fixtures
{
	public enum WidgetKind
	{
		Basic,
		Fancy
	}

	public class Widget
	{
		public required string Name { get; set; }
		public WidgetKind Kind { get; set; }
	}

	public class Paged<T>
	{
		public List<T> Items { get; set; } = [];
		public int Total { get; set; }
	}

	[SvelteComponent]
	public class CardViewModel
	{
		public required string Heading { get; set; }
		public List<Widget> Widgets { get; set; } = [];
	}

	[SvelteRemote]
	public class WidgetApi
	{
		[Query] public List<Widget> Search(string term, int limit) => [];
		[Command] public void Clear() { }
		[Form] public Task<int> Save(Widget widget) => Task.FromResult(0);
	}

	[SvelteRemote]
	public class CatalogApi
	{
		[Query] public List<Widget> Search(string term) => [];
	}
}

// Two types called Item in different namespaces, plus models whose short names would
// shadow DOM globals, plus one renamed with [SvelteType]. All four cases have to survive
// in the same ambient declaration block.
namespace SvelteNet.AspNetCore.Tests.Fixtures.Orders
{
	public class Item
	{
		public required string Sku { get; set; }
	}

	// `File` and `Event` are declared by lib.dom.d.ts.
	public class File
	{
		public required string Path { get; set; }
	}
}

namespace SvelteNet.AspNetCore.Tests.Fixtures.Catalog
{
	public class Item
	{
		public required string Title { get; set; }
	}

	[SvelteType("CatalogEvent")]
	public class Event
	{
		public required string Name { get; set; }
	}
}

namespace SvelteNet.AspNetCore.Tests.Fixtures.Naming
{
	[SvelteRemote]
	public class NamingApi
	{
		[Query] public Orders.Item GetOrderItem() => new() { Sku = "x" };
		[Query] public Catalog.Item GetCatalogItem() => new() { Title = "y" };
		[Query] public Orders.File GetFile() => new() { Path = "/tmp" };
		[Query] public Catalog.Event GetEvent() => new() { Name = "e" };
	}
}

namespace SvelteNet.AspNetCore.Tests.Fixtures.Pages
{
	using SvelteNet.AspNetCore.Tests.Fixtures;

	public class HomeModel : SveltePage
	{
		[SvelteProp] public string Title { get; set; } = string.Empty;
		[SvelteProp] public List<Widget> Widgets { get; set; } = [];
	}
}

namespace SvelteNet.AspNetCore.Tests.Fixtures.Pages.Admin
{
	using SvelteNet.AspNetCore.Tests.Fixtures;

	public class UsersModel : SveltePage
	{
		[SvelteProp] public Paged<Widget> Users { get; set; } = new();
	}
}

namespace SvelteNet.AspNetCore.Tests.Fixtures.Features.Inventory
{
	[SvelteRemote]
	public class InventoryApi
	{
		[Query] public int GetStock() => 3;
	}
}
