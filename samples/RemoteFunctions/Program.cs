namespace RemoteFunctions;

using FluentValidation;
using RemoteFunctions.Features.Todos;
using SvelteNet.AspNetCore;
using SvelteNet.FluentValidation;

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
		builder.Services
			.AddSvelteNet(options => options.PagesPath = "Features")
			.AddJintSSR();

		// FluentValidation plugs into the remote-function validation pipeline;
		// registered IValidator<T>s run automatically before dispatch.
		builder.Services.AddSvelteNetFluentValidation();
		builder.Services.AddScoped<IValidator<Feedback>, FeedbackValidator>();

		var app = builder.Build();

		app.UseStaticFiles();
		app.UseSvelteNet();
		app.MapRazorPages();

		app.Run();
	}
}
