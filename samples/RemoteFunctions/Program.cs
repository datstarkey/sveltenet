namespace RemoteFunctions;

using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

		// Ordinary ASP.NET authentication and authorization — SvelteNet does not
		// introduce its own. AdminApi's [Authorize(Policy = "admin")] is enforced
		// through these, over HTTP and during SSR alike.
		builder.Services
			.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
			.AddCookie(options => options.LoginPath = "/");
		builder.Services.AddAuthorizationBuilder()
			.AddPolicy("admin", policy => policy.RequireRole("admin"));

		var app = builder.Build();

		app.UseStaticFiles();
		app.UseAuthentication();
		app.UseAuthorization();
		app.UseSvelteNet();
		app.MapRazorPages();

		// Demo sign-in so the authorization sample is runnable. A real app would
		// validate credentials here.
		app.MapPost("/demo-login", async (HttpContext context, string role) =>
		{
			var identity = new ClaimsIdentity(
				[new Claim(ClaimTypes.Name, "demo"), new Claim(ClaimTypes.Role, role)],
				CookieAuthenticationDefaults.AuthenticationScheme);
			await context.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				new ClaimsPrincipal(identity));
			return Results.Ok();
		});

		app.Run();
	}
}
