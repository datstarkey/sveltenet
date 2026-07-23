namespace MvcHello;

using SvelteNet.AspNetCore;

// Explicit, namespaced entry point (instead of top-level statements) so
// WebApplicationFactory<MvcHello.Program> is unambiguous alongside the
// other samples in the integration-test project.
public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddControllersWithViews();
		builder.Services.AddSvelteNet();

		var app = builder.Build();

		app.UseStaticFiles();
		app.UseSvelteNet();
		app.MapControllerRoute("default", "{controller=Hello}/{action=Index}/{id?}");

		app.Run();
	}
}
