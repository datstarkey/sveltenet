﻿namespace Svelte.Net.Builders;

using Core;
using Core.Attributes;
using Core.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using SkbKontur.TypeScript.ContractGenerator;
using SkbKontur.TypeScript.ContractGenerator.Abstractions;
using SkbKontur.TypeScript.ContractGenerator.CodeDom;
using SkbKontur.TypeScript.ContractGenerator.TypeBuilders;

public static class SvelteNetServices
{
	public static void AddSvelteNet(this IServiceCollection services, Action<SvelteOptions>? options = null)
	{
		SvelteNet.Configure(options);
		services.AddSingleton(SvelteNet.Options);
		services.AddSingleton<SvelteService>();
	}
}

public static class SvelteNetAppBuilder
{
	
	
	
	public static IApplicationBuilder UseSvelteNetDevServer(this IApplicationBuilder app, string? devServer = null)
	{

		//Dont dev in a container
		if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
		{
			return app;
		}
		
		SvelteNet.Options.IsDev = true;
		SvelteNet.Options.DevServer = devServer ?? "http://localhost:3000";
		
		using var scope = app.ApplicationServices.CreateScope();

		var svelteLocation = Path.Join(Directory.GetCurrentDirectory(), SvelteNet.Options.PagesPath);

		
		//Generate Binded Types
		var propertyTypes = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(s => s.GetTypes())
			.Where(s => s.IsSubclassOf(typeof(SveltePage)))
			.SelectMany(s => s.GetProperties()
				.Where(p => p.GetCustomAttributes(typeof(SvelteBindAttribute), true).Any())
				.ToList()
			)
			.Select(s=>s.PropertyType)
			.Distinct()
			.ToList();
		
		propertyTypes.Add(typeof(ModelStateEntry));

		var allReferenedTypes = propertyTypes.GetAllTypes();
		var allInterfaces = allReferenedTypes.Select(s => s.GetTypescriptInterface(false));
		

		var contracts = $@"
/* eslint-disable */
// This file is automatically generated by SvelteNet

{string.Join(Environment.NewLine,allInterfaces)}
";
		
		File.WriteAllText(svelteLocation + "/types.ts", contracts);
		
		// var generator = new TypeScriptGenerator(TypeScriptGenerationOptions.Default, new TypescriptTypeGenerator(), new RootTypesProvider(propertyTypes.ToArray()));
		// generator.GenerateFiles("./" + SvelteNet.Options.PagesPath);
		// scope.Dispose();

		
		
		//Generate Local Types && Svelte Pages
		var customPageTypes = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(s => s.GetTypes())
			.Where(s => s.IsSubclassOf(typeof(SveltePage)))
			.ToArray();



		foreach (var type in customPageTypes)
		{
			var path = type.GetPathFromNameSpace();
			var fullPath = Path.Join(Directory.GetCurrentDirectory(), SvelteNet.Options.PagesPath, path);
			if (!Directory.Exists(fullPath))
			{
				Directory.CreateDirectory(fullPath);
			}
			
			var properties = type.GetProperties()
				.Where(p => p.GetCustomAttributes(typeof(SvelteBindAttribute), true).Any())
				.ToDictionary(p => p.Name, p => p.PropertyType.TsType());

			var importProps = type.GetProperties()
				.Where(p => p.GetCustomAttributes(typeof(SvelteBindAttribute), true).Any())
				.Where(p => !p.PropertyType.IsSimpleType())
				.Select( p => p.PropertyType.TsType().Replace("[]", "").Split("<").First())
				.Distinct().ToList();

			
			var name = type.GetNameFromType();
			var typesFile = fullPath + name+".ts";
			var svelteFile = fullPath + name+".svelte";
			var allTypesFile = Path.Join(svelteLocation, "types.ts");

			var resolvedPath = Path.GetRelativePath(fullPath,allTypesFile).Replace("\\", "/");
			if(!resolvedPath.StartsWith("../")) resolvedPath = "./" + resolvedPath;
			importProps.Add("ModelStateEntry");
			var importString = string.Join(", ", importProps);
			
			File.WriteAllText(typesFile, $@"
import type {{ {importString} }} from ""{resolvedPath.Replace(".ts","")}"";
export interface {name}Data {{
	{string.Join(Environment.NewLine+"    ", properties.Select(p => $"{p.Key.ToCamelCase()}: {p.Value};"))}
    modelState: Record<string, ModelStateEntry>;
}}
");
			if (!File.Exists(svelteFile))
			{
				File.WriteAllText(svelteFile,$@"
<script lang=""ts"">
    import type {{{name}Data}} from ""./{name}"";
    export let data: {name}Data;
</script>
");
			}
		}
		
		//Generate Utils Types
		var utilFile = Path.Join(svelteLocation, "utils.d.ts");
		var pagesCs = Path.Join(Directory.GetCurrentDirectory(), "Pages");
		var paths = Directory.GetFiles(pagesCs, "*.cshtml", SearchOption.AllDirectories)
			.Select(s => s.Replace(pagesCs, "").Replace("\\", "/").Replace(".cshtml", "").Replace("Index",""))
			.Where(f=>!f.StartsWith("_") || !f.StartsWith("/_"))
			.ToList();
		
		File.WriteAllText(utilFile, $@"
declare global {{
    type RouteId  = {string.Join(" | ", paths.Select(p=>$@"""{p}"""))} | undefined | string & {{}}; 
}}
export {{}}
");
		
		
		return app;
	}

}

public static class SveltePageTypeWriter
{

	
	public static string GetNameFromType(this Type type)
	{
			return type.Name.TrimEnd("Model".ToCharArray());
	}
	
	public static string GetPathFromNameSpace(this Type type)
	{
		var path = type.Namespace?.Split(".Pages").Last().Trim();
		path = path?.Replace(".", "/");
			path += "/";
		return path;
	}
	

}

public class TypescriptTypeGenerator : ICustomTypeGenerator
{

	private readonly string _fileName;

	public TypescriptTypeGenerator(string fileName = "types")
	{
		_fileName = fileName;
	}

	public string GetTypeLocation(ITypeInfo type)
	{
		return _fileName;// without extension! .ts will be added automatically.
	}

	public TypeScriptTypeMemberDeclaration? ResolveProperty(TypeScriptUnit unit, ITypeGenerator typeGenerator, ITypeInfo type, IPropertyInfo property)
	{
		return null;
	}

	public ITypeBuildingContext? ResolveType(string initialUnitPath, ITypeGenerator typeGenerator, ITypeInfo type, ITypeScriptUnitFactory unitFactory)
	{
		return null;
	}
}