namespace SvelteNet.Core.Tests.Fixtures;

public enum OrderStatus
{
	PendingApproval,
	Shipped
}

public class Address
{
	public required string Street { get; set; }
	public string? City { get; set; }
}

public class Customer
{
	public required string Name { get; set; }
	public Address? Address { get; set; }
}

public class OrderLine
{
	public int Quantity { get; set; }
	public decimal Price { get; set; }
}

public class Order
{
	public Guid Id { get; set; }
	public DateTime CreatedAt { get; set; }
	public OrderStatus Status { get; set; }
	public List<OrderLine> Lines { get; set; } = [];
	public Dictionary<string, Customer> Customers { get; set; } = [];
}

public class Paged<T>
{
	public List<T> Items { get; set; } = [];
	public int Total { get; set; }
}
