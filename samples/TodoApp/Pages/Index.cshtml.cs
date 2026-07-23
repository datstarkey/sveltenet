namespace TodoApp.Pages;

using Microsoft.AspNetCore.Mvc;
using SvelteNet;
using SvelteNet.AspNetCore;
using TodoApp.Models;
using TodoApp.Services;

public class IndexModel(TodoStore store) : SveltePage
{
	[SvelteProp] public string Title { get; set; } = "SvelteNet Todos";
	[SvelteProp] public IReadOnlyList<Todo> Todos { get; set; } = [];

	[BindProperty] public string? NewLabel { get; set; }
	[BindProperty] public Priority NewPriority { get; set; }

	public void OnGet() => Todos = store.All;

	public IActionResult OnPost()
	{
		if (string.IsNullOrWhiteSpace(NewLabel))
		{
			ModelState.AddModelError(nameof(NewLabel), "A label is required.");
			Todos = store.All;
			return Page();
		}

		store.Add(NewLabel.Trim(), NewPriority);
		return RedirectToPage();
	}

	public IActionResult OnPostToggle(int id)
	{
		store.Toggle(id);
		return RedirectToPage();
	}
}
