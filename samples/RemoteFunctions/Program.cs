namespace RemoteFunctions;

using RemoteFunctions.Services;
using SvelteNet.AspNetCore;

// Explicit, namespaced entry point (instead of top-level statements) so
// WebApplicationFactory<RemoteFunctions.Program> is unambiguous alongside the
// other samples in the integration-test project.
public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddRazorPages();
		builder.Services.AddSingleton<TodoStore>();
		builder.Services.AddSvelteNet();

		var app = builder.Build();

		app.UseStaticFiles();
		app.UseSvelteNet();
		app.MapRazorPages();

		app.Run();
	}
}
