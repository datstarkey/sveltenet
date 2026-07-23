// Invoked by SvelteNet.Build.targets after each build:
//   dotnet exec SvelteNet.Build.dll <app-dll> <project-dir>
// Loads the app assembly (and its SvelteNet from the same output directory) in an
// isolated AssemblyLoadContext, then runs the scaffolder — so `dotnet build` keeps
// Svelte/types.ts, *.types.ts, remote.ts, and routes.d.ts in sync without the app
// ever having to run.
using System.Reflection;
using System.Runtime.Loader;

if (args.Length < 2)
{
	Console.Error.WriteLine("Usage: SvelteNet.Build <app-dll> <project-dir>");
	return 1;
}

var appPath = Path.GetFullPath(args[0]);
var projectDir = Path.GetFullPath(args[1]);

if (!File.Exists(appPath))
{
	Console.Error.WriteLine($"SvelteNet.Build: assembly not found: {appPath}");
	return 1;
}

var resolver = new AssemblyDependencyResolver(appPath);
var context = new AssemblyLoadContext("SvelteNetTypeGen");
context.Resolving += (loadContext, name) =>
{
	var path = resolver.ResolveAssemblyToPath(name);
	return path is null ? null : loadContext.LoadFromAssemblyPath(path);
};

try
{
	var app = context.LoadFromAssemblyPath(appPath);
	var core = context.LoadFromAssemblyName(new AssemblyName("SvelteNet.Core"));
	var aspNetCore = context.LoadFromAssemblyName(new AssemblyName("SvelteNet.AspNetCore"));

	var optionsType = core.GetType("SvelteNet.SvelteOptions")
		?? throw new InvalidOperationException("SvelteNet.SvelteOptions not found in SvelteNet.Core.");
	var options = Activator.CreateInstance(optionsType)!;
	optionsType.GetProperty("ContentRoot")!.SetValue(options, projectDir);
	optionsType.GetProperty("ApplicationAssemblies")!.SetValue(options, new[] { app });

	var scaffolder = aspNetCore.GetType("SvelteNet.AspNetCore.Dev.SvelteScaffolder")
		?? throw new InvalidOperationException("SvelteScaffolder not found in SvelteNet.AspNetCore.");
	scaffolder.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
		.Invoke(null, [options, null, null, null]);

	var pagesPath = (string)optionsType.GetProperty("PagesPath")!.GetValue(options)!;
	Console.WriteLine($"SvelteNet: generated TypeScript in {Path.Combine(projectDir, pagesPath)}");
	return 0;
}
catch (TargetInvocationException e) when (e.InnerException is not null)
{
	Console.Error.WriteLine($"SvelteNet.Build: {e.InnerException.Message}");
	return 1;
}
