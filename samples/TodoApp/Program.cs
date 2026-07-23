using SvelteNet.AspNetCore;
using TodoApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddSvelteNet();

var app = builder.Build();

app.UseStaticFiles();
app.UseSvelteNet();
app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;
