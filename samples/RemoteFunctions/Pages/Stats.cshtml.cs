namespace RemoteFunctions.Pages;

using RemoteFunctions.Services;
using SvelteNet;
using SvelteNet.AspNetCore;

public class StatsModel(TodoStore store) : SveltePage
{
	[SvelteProp] public int Total { get; set; }
	[SvelteProp] public int Completed { get; set; }
	[SvelteProp] public Dictionary<string, int> ByPriority { get; set; } = [];

	public void OnGet()
	{
		var todos = store.All;
		Total = todos.Count;
		Completed = todos.Count(t => t.Done);
		ByPriority = todos
			.GroupBy(t => t.Priority.ToString())
			.ToDictionary(g => g.Key, g => g.Count());
	}
}
