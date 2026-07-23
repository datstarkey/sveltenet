namespace RemoteFunctions.Models;

public enum Priority
{
	Low,
	Medium,
	High
}

public class Todo
{
	public int Id { get; set; }
	public required string Label { get; set; }
	public bool Done { get; set; }
	public Priority Priority { get; set; }
}
