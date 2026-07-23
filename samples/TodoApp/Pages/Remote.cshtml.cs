namespace TodoApp.Pages;

using SvelteNet.AspNetCore;

/// <summary>The component drives everything through remote functions — no page props needed.</summary>
public class RemoteModel : SveltePage
{
	protected override SvelteNet.ComponentOptions SvelteComponentOptions()
	{
		var options = base.SvelteComponentOptions();
		// Await-expression components need fetch, which the SSR engine doesn't have.
		options.Ssr = false;
		return options;
	}
}
