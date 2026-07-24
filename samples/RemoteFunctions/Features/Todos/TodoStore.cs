namespace RemoteFunctions.Features.Todos;

public class TodoStore
{
	private readonly Lock _lock = new();
	private int _nextId = 4;

	private readonly List<Todo> _todos =
	[
		new() { Id = 1, Label = "Rewrite for Svelte 5", Done = true, Priority = Priority.High },
		new() { Id = 2, Label = "Verify Jint SSR", Done = true, Priority = Priority.High },
		new() { Id = 3, Label = "Ship it", Priority = Priority.Medium }
	];

	public IReadOnlyList<Todo> All
	{
		get
		{
			lock (_lock) return [.. _todos];
		}
	}

	public void Add(string label, Priority priority)
	{
		lock (_lock) _todos.Add(new Todo { Id = _nextId++, Label = label, Priority = priority });
	}

	public void Toggle(int id)
	{
		lock (_lock)
		{
			var todo = _todos.FirstOrDefault(t => t.Id == id);
			if (todo != null) todo.Done = !todo.Done;
		}
	}
}
