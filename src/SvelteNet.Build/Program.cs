// Invoked by SvelteNet.Build.targets after each build:
//   dotnet exec SvelteNet.Build.dll <app-dll> <project-dir> <pages-path>
//       <client-output> <server-output> [application-assembly-paths]
// Loads the app assembly (and its SvelteNet from the same output directory) in an
// isolated AssemblyLoadContext, then runs the scaffolder — so `dotnet build` keeps
// .svelte-net declarations and colocated *.remote.ts clients in sync without the app
// ever having to run.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length < 5)
{
	Console.Error.WriteLine(
		"Usage: SvelteNet.Build <app-dll> <project-dir> <pages-path> <client-output> <server-output> [application-assembly-paths]");
	return 1;
}

var appPath = Path.GetFullPath(args[0]);
var projectDir = Path.GetFullPath(args[1]);
var pagesPathOption = args[2];
var clientOutput = args[3];
var serverOutput = args[4];

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
	var configuredAssemblyPaths = args.Length > 5
		? args[5].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		: [];
	var applicationAssemblyPaths = configuredAssemblyPaths.Length == 0 ? [appPath] : configuredAssemblyPaths;
	var applicationAssemblies = applicationAssemblyPaths
		.Select(Path.GetFullPath)
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.Select(path => string.Equals(path, appPath, StringComparison.OrdinalIgnoreCase)
			? app
			: context.LoadFromAssemblyPath(path))
		.ToArray();
	foreach (var assembly in applicationAssemblies)
	{
		RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
		var metadataType = assembly.GetType("SvelteNet.Generated.__SvelteNetMetadata");
		var metadataInit = metadataType?.GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static);
		metadataInit?.Invoke(null, null);
	}
	var core = context.LoadFromAssemblyName(new AssemblyName("SvelteNet.Core"));
	var aspNetCore = context.LoadFromAssemblyName(new AssemblyName("SvelteNet.AspNetCore"));

	var optionsType = core.GetType("SvelteNet.SvelteOptions")
		?? throw new InvalidOperationException("SvelteNet.SvelteOptions not found in SvelteNet.Core.");
	var options = Activator.CreateInstance(optionsType)!;
	optionsType.GetProperty("ContentRoot")!.SetValue(options, projectDir);
	optionsType.GetProperty("PagesPath")!.SetValue(options, pagesPathOption);
	optionsType.GetProperty("ClientOutput")!.SetValue(options, clientOutput);
	optionsType.GetProperty("ServerOutput")!.SetValue(options, serverOutput);
	optionsType.GetProperty("ApplicationAssemblies")!.SetValue(options, applicationAssemblies);

	var scaffolder = aspNetCore.GetType("SvelteNet.AspNetCore.Dev.SvelteScaffolder")
		?? throw new InvalidOperationException("SvelteScaffolder not found in SvelteNet.AspNetCore.");
	scaffolder.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
		.Invoke(null, [options, null, null, null]);

	Console.WriteLine($"SvelteNet: generated TypeScript in {Path.Combine(projectDir, pagesPathOption)}");
	return 0;
}
catch (TargetInvocationException e) when (e.InnerException is not null)
{
	Console.Error.WriteLine($"SvelteNet.Build: {e.InnerException.Message}");
	return 1;
}
