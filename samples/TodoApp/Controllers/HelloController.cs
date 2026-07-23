namespace TodoApp.Controllers;

using Microsoft.AspNetCore.Mvc;

public record HelloViewModel(string Name, int Visits);

/// <summary>MVC example — the component and its data are named explicitly in the view.</summary>
public class HelloController : Controller
{
	public IActionResult Index() => View(new HelloViewModel("SvelteNet", Random.Shared.Next(1, 100)));
}
