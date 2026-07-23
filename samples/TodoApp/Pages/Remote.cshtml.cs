namespace TodoApp.Pages;

using SvelteNet.AspNetCore;

/// <summary>
/// The component drives everything through remote functions — no page props needed.
/// Its awaited queries run during SSR too, via the in-process fetch bridge.
/// </summary>
public class RemoteModel : SveltePage
{
}
