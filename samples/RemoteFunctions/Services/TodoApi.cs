namespace RemoteFunctions.Services;

using RemoteFunctions.Models;
using SvelteNet;

public record TodoStats(int Total, int Completed, Dictionary<string, int> ByPriority);

/// <summary>
/// Remote functions, SvelteKit-style: [Query] reads, [Command] writes, [Form]
/// form handlers. Callable from Svelte through the generated typed client
/// (Svelte/remote.ts). Dispatch is compiled by SvelteNet.Generators.
/// </summary>
[SvelteRemote]
public class TodoApi(TodoStore store)
{
	[Query]
	public IReadOnlyList<Todo> GetTodos() => store.All;

	[Query]
	public TodoStats GetStats()
	{
		var todos = store.All;
		return new TodoStats(
			todos.Count,
			todos.Count(t => t.Done),
			todos.GroupBy(t => t.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count()));
	}

	[Command]
	public void ToggleTodo(int id) => store.Toggle(id);

	[Form]
	public async Task<Todo> CreateTodo(string label, Priority priority)
	{
		if (string.IsNullOrWhiteSpace(label))
			throw new RemoteInvalidException(nameof(label), "A label is required.");

		await Task.Yield();
		store.Add(label.Trim(), priority);
		return store.All[^1];
	}
}
