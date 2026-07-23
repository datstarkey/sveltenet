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
