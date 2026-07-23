namespace TodoApp.Controllers;

using Microsoft.AspNetCore.Mvc;
using SvelteNet;

// [SvelteComponent] binds this model to Svelte/Components/Hello.svelte
// (convention: "Components/" + type name minus the ViewModel suffix), so views
// render it with @Html.Svelte(Model) — no component string, and the model's
// TypeScript interface is generated into Svelte/types.ts.
[SvelteComponent]
public record HelloViewModel(string Name, int Visits);

public class HelloController : Controller
{
	public IActionResult Index() => View(new HelloViewModel("SvelteNet", Random.Shared.Next(1, 100)));
}
