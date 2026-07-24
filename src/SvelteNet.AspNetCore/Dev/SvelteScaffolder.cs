namespace SvelteNet.AspNetCore.Dev;

using System.Reflection;
using System.Text.Json.Serialization;
using SvelteNet.Generated;
using SvelteNet.Remote;
using SvelteNet.TypeGen;

/// <summary>
/// Build-time code generation: ambient TypeScript declarations under .svelte-net,
/// typed remote clients, scaffold components, and Vite config. Generated files are
/// overwritten each run; user-owned files are written only when missing.
/// </summary>
public static class SvelteScaffolder
{
	private sealed record PropertySource(
		string JsonName,
		Type Type,
		string TypeScriptType,
		string[] Imports);

	private sealed record PageSource(Type Type, string Component, PropertySource[] Properties);

	private sealed record ComponentSource(Type Type, string Component);

	public static void Run(SvelteOptions options, IReadOnlyList<Type>? pageTypes = null, IReadOnlyList<Type>? componentModelTypes = null, IReadOnlyList<Type>? remoteServiceTypes = null)
	{
		var svelteDir = Path.Combine(options.ContentRoot, options.PagesPath);
		var generatedDir = Path.Combine(options.ContentRoot, ".svelte-net");
		var generatedTypesDir = Path.Combine(generatedDir, "types");
		Directory.CreateDirectory(svelteDir);
		Directory.CreateDirectory(generatedDir);
		File.Delete(Path.Combine(generatedDir, "types.d.ts"));
		File.Delete(Path.Combine(generatedDir, "routes.d.ts"));
		if (Directory.Exists(generatedTypesDir)) Directory.Delete(generatedTypesDir, recursive: true);
		Directory.CreateDirectory(generatedTypesDir);

		var scope = options.ApplicationAssemblies;
		var explicitTypes = pageTypes is not null || componentModelTypes is not null || remoteServiceTypes is not null;
		var pages = pageTypes is not null
			? pageTypes.Select(ReflectionPage).ToList()
			: SvelteGeneratedMetadata.GetPages(scope)
				.Select(p => new PageSource(
					p.PageType,
					p.Component,
					p.Properties.Select(x => new PropertySource(x.JsonName, x.Type, x.TypeScriptType, x.Imports)).ToArray()))
				.ToList();
		var componentModels = componentModelTypes is not null
			? componentModelTypes.Select(t => new ComponentSource(t, SvelteComponentResolver.Resolve(t, enableReflectionFallback: true))).ToList()
			: SvelteGeneratedMetadata.GetComponents(scope).Select(c => new ComponentSource(c.ModelType, c.Component)).ToList();
		var remoteServices = remoteServiceTypes is not null
			? remoteServiceTypes.Select(SvelteRemoteDescriptors.For).ToList()
			: SvelteRemoteDescriptors.All.Where(d => InScope(d.ServiceType.Assembly, scope)).ToList();

		if (options.EnableReflectionFallback)
		{
			if (pages.Count == 0)
				pages = TypeDiscovery.FindTypes(scope, t => t.IsSubclassOf(typeof(SveltePage)) && !t.IsAbstract)
					.Select(ReflectionPage).ToList();
			if (componentModels.Count == 0)
				componentModels = TypeDiscovery.FindTypes(scope, t => t.IsDefined(typeof(SvelteComponentAttribute), false))
					.Select(t => new ComponentSource(t, SvelteComponentResolver.Resolve(t, enableReflectionFallback: true))).ToList();
			var registeredTypes = remoteServices.Select(d => d.ServiceType).ToHashSet();
			remoteServices.AddRange(TypeDiscovery.FindTypes(
					scope,
					t => t.IsDefined(typeof(SvelteRemoteAttribute), false) && !registeredTypes.Contains(t))
				.Select(SvelteRemoteDescriptors.FromReflection));
		}

		var generatedTypes = explicitTypes ? [] : SvelteGeneratedMetadata.GetTypes(scope);
		WriteSharedTypes(generatedTypesDir, svelteDir, generatedTypes, pages, componentModels, remoteServices);
		foreach (var page in pages)
		{
			WritePageTypes(generatedTypesDir, options.PagesPath, page);
			ScaffoldPage(svelteDir, page);
		}
		foreach (var model in componentModels) ScaffoldComponentModel(svelteDir, model);
		WriteRemoteClient(svelteDir, remoteServices);

		WriteIfMissing(Path.Combine(options.ContentRoot, "vite.config.ts"), SvelteTemplates.ViteConfig(options));
		WriteIfMissing(Path.Combine(options.ContentRoot, "tsconfig.json"), SvelteTemplates.TsConfig(options));
		WriteIfMissing(Path.Combine(options.ContentRoot, "package.json"), SvelteTemplates.PackageJson);
		WriteRouteIds(options, generatedTypesDir, svelteDir);
	}

	private static bool InScope(Assembly assembly, IReadOnlyList<Assembly>? scope) =>
		scope is null or { Count: 0 } || scope.Contains(assembly);

	private static PageSource ReflectionPage(Type page)
	{
		var nullability = new NullabilityInfoContext();
		var properties = page.GetProperties()
			.Where(p => p.IsDefined(typeof(SveltePropAttribute), true))
			.Select(p =>
			{
				var imports = new SortedSet<string>(StringComparer.Ordinal);
				CollectImports(p.PropertyType, imports);
				var nullable = nullability.Create(p).ReadState == NullabilityState.Nullable;
				return new PropertySource(
					p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name.ToCamelCase(),
					p.PropertyType,
					p.PropertyType.TsType() + (nullable ? " | null" : ""),
					imports.ToArray());
			})
			.ToArray();
		return new PageSource(page, Path.Combine(PageDirectory(page), PageName(page)).Replace('\\', '/'), properties);
	}

	private static void WriteSharedTypes(
		string generatedTypesDir,
		string svelteDir,
		IReadOnlyList<SvelteGeneratedType> generatedTypes,
		List<PageSource> pages,
		List<ComponentSource> componentModels,
		List<RemoteServiceDescriptor> remoteServices)
	{
		IEnumerable<string> interfaces;
		if (generatedTypes.Count > 0)
		{
			var duplicate = generatedTypes
				.GroupBy(t => t.TypeScriptName, StringComparer.Ordinal)
				.FirstOrDefault(g => g.Select(t => t.Type).Distinct().Count() > 1);
			if (duplicate is not null)
				throw new InvalidOperationException(
					$"Generated TypeScript name '{duplicate.Key}' is used by multiple CLR types: " +
					string.Join(", ", duplicate.Select(d => d.Type.FullName)));
			interfaces = generatedTypes
				.GroupBy(t => t.Type)
				.Select(g => g.First())
				.OrderBy(t => t.TypeScriptName, StringComparer.Ordinal)
				.Select(t => t.Declaration);
		}
		else
		{
			var remoteTypes = remoteServices
				.SelectMany(d => d.Methods)
				.SelectMany(m => m.Parameters.Select(p => p.Type).Append(m.ReturnType))
				.Where(t => t != typeof(void) && t != typeof(CancellationToken));

			var rootTypes = pages.SelectMany(p => p.Properties)
				.Select(p => p.Type)
				.Concat(componentModels.Select(c => c.Type))
			.Concat(remoteTypes)
			.Distinct()
			.ToList();

			interfaces = rootTypes.GetAllTypes()
				.Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t)
				.Distinct()
				.Select(t => t.GetTypescriptInterface(false));
		}

		var duplicatePage = pages
			.GroupBy(PageDataType, StringComparer.Ordinal)
			.FirstOrDefault(g => g.Count() > 1);
		if (duplicatePage is not null)
			throw new InvalidOperationException(
				$"Generated page data type '{duplicatePage.Key}' is used by multiple pages: " +
				string.Join(", ", duplicatePage.Select(p => p.Type.FullName)));

		var declarations = interfaces.Select(ToAmbientDeclaration);

		File.Delete(Path.Combine(svelteDir, "types.ts"));
		File.Delete(Path.Combine(svelteDir, "types.d.ts"));
		File.WriteAllText(Path.Combine(generatedTypesDir, "models.d.ts"), $$"""
/* eslint-disable */
// Generated by SvelteNet — do not edit.

declare global {
{{string.Join(Environment.NewLine + Environment.NewLine, declarations.Select(Indent))}}
}

export {};
""" + Environment.NewLine);
	}

	private static void WritePageTypes(string generatedTypesDir, string sourceRoot, PageSource page)
	{
		var outputPath = Path.Combine(generatedTypesDir, sourceRoot, page.Component + ".d.ts");
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
		File.WriteAllText(outputPath, $$"""
// Generated by SvelteNet — do not edit.
declare global {
{{Indent(PageDeclaration(page))}}
}

export {};
""" + Environment.NewLine);
	}

	private static void ScaffoldPage(string svelteDir, PageSource page)
	{
		var name = Path.GetFileName(page.Component);
		var directory = Path.GetDirectoryName(page.Component) ?? string.Empty;
		var dir = Path.Combine(svelteDir, directory);
		Directory.CreateDirectory(dir);

		File.Delete(Path.Combine(dir, $"{name}.types.ts"));
		File.Delete(Path.Combine(dir, $"{name}.types.d.ts"));

		WriteIfMissing(Path.Combine(dir, $"{name}.svelte"), SvelteTemplates.Page(PageDataType(page)));
	}

	/// <summary>
	/// Scaffolds the component for a [SvelteComponent] model. Its data interface is
	/// the ambient model type emitted under .svelte-net.
	/// </summary>
	private static void ScaffoldComponentModel(string svelteDir, ComponentSource model)
	{
		var component = model.Component;
		var path = Path.Combine(svelteDir, component + ".svelte");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		WriteIfMissing(path, SvelteTemplates.ComponentModelPage(model.Type.TsType()));
	}

	/// <summary>
	/// Generates one typed client class per remote service, colocated with its vertical
	/// feature when the service namespace contains ".Features.".
	/// </summary>
	private static void WriteRemoteClient(string svelteDir, List<RemoteServiceDescriptor> remoteServices)
	{
		foreach (var path in Directory.GetFiles(svelteDir, "*.remote.ts", SearchOption.AllDirectories)
					 .Concat(Directory.GetFiles(svelteDir, "remote.ts", SearchOption.AllDirectories)))
		{
			if (File.Exists(path) && File.ReadAllText(path).Contains("Generated by SvelteNet", StringComparison.Ordinal))
				File.Delete(path);
		}

		foreach (var service in remoteServices) WriteRemoteServiceClient(svelteDir, service);
	}

	private static void WriteRemoteServiceClient(string svelteDir, RemoteServiceDescriptor service)
	{
		var featureDirectory = FeatureDirectory(service.ServiceType);
		var outputDirectory = Path.Combine(svelteDir, featureDirectory);
		Directory.CreateDirectory(outputDirectory);
		var outputPath = Path.Combine(outputDirectory, $"{service.Name}.remote.ts");
		var usedKinds = new SortedSet<string>(StringComparer.Ordinal);
		var members = new List<string>();
		var methods = service.Methods
			.OrderBy(x => x.Name, StringComparer.Ordinal)
			.ToList();

		foreach (var method in methods)
		{
			var tsReturn = method.TypeScriptReturnType ??
						   (method.ReturnType == typeof(void) ? "void" : method.ReturnType.TsType());
			var name = method.Name;
			var path = $"{service.Name}/{method.Name}";

			var expression = method.Kind switch
			{
				RemoteKind.Form => FormExpression(path, tsReturn, method.Parameters),
				_ => CallableExpression(method.Kind == RemoteKind.Query ? "query" : "command", path, tsReturn, method.Parameters)
			};
			members.Add($"\tstatic readonly {name} = {expression};");
			usedKinds.Add(method.Kind switch
			{
				RemoteKind.Query => "query",
				RemoteKind.Command => "command",
				_ => "form"
			});
		}

		var runtimeImport = usedKinds.Count == 0
			? string.Empty
			: $"import {{ {string.Join(", ", usedKinds)} }} from 'sveltenet/remote';{Environment.NewLine}";

		File.WriteAllText(outputPath, $$"""
/* eslint-disable */
// Generated by SvelteNet — do not edit.
{{runtimeImport}}
export class {{service.Name}} {
{{string.Join(Environment.NewLine, members)}}
}
""" + Environment.NewLine);
	}

	private static string FeatureDirectory(Type serviceType)
	{
		var ns = serviceType.Namespace ?? string.Empty;
		const string marker = ".Features.";
		var index = ns.IndexOf(marker, StringComparison.Ordinal);
		return index < 0 ? string.Empty : ns[(index + marker.Length)..].Replace('.', Path.DirectorySeparatorChar);
	}

	private static string PageDeclaration(PageSource page)
	{
		var fields = page.Properties
			.Select(p => $"\t{p.JsonName}: {p.TypeScriptType};")
			.Append("\t/** RFC 9457 validation problem (ASP.NET `errors` member); null when the model state is valid. */")
			.Append("\tproblem: { title: string; status: number; errors: Record<string, string[]> } | null;")
			.Append("\tantiforgeryToken: string;");
		return $$"""
interface {{PageDataType(page)}} {
{{string.Join(Environment.NewLine, fields)}}
}
""";
	}

	private static string PageDataType(PageSource page) =>
		string.Concat(page.Component.Split('/').Select(TypeIdentifier)) + "Data";

	private static string TypeIdentifier(string value)
	{
		var identifier = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
		if (identifier.Length == 0) return "_";
		return char.IsDigit(identifier[0]) ? "_" + identifier : identifier;
	}

	private static string ToAmbientDeclaration(string declaration) =>
		declaration.StartsWith("export ", StringComparison.Ordinal)
			? declaration["export ".Length..]
			: declaration;

	private static string Indent(string declaration) =>
		string.Join(Environment.NewLine, declaration.Split(Environment.NewLine).Select(line => "\t" + line));

	private static string CallableExpression(string kind, string path, string tsReturn, SvelteNet.Remote.RemoteParameter[] parameters)
	{
		if (parameters.Length == 0)
			return $"{kind}<{tsReturn}>('{path}')";

		var tuple = string.Join(", ", parameters.Select(p => $"{p.Name}: {p.TypeScriptType ?? p.Type.TsType()}"));
		var names = string.Join(", ", parameters.Select(p => p.Name));
		return $"{kind}<{tsReturn}, [{tuple}]>('{path}', ({names}) => ({{ {names} }}))";
	}

	private static string FormExpression(string path, string tsReturn, SvelteNet.Remote.RemoteParameter[] parameters)
	{
		var fields = string.Join("; ", parameters.Select(p => $"{p.Name}: {p.TypeScriptType ?? p.Type.TsType()}"));
		var fieldsType = parameters.Length == 0 ? "{}" : $"{{ {fields} }}";
		return $"form<{tsReturn}, {fieldsType}>('{path}')";
	}

	/// <summary>Collects the named types a page's data interface must import from the shared types file.</summary>
	private static void CollectImports(Type? type, ISet<string> names)
	{
		if (type is null) return;
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type.IsGenericParameter || type.IsSimpleType()) return;

		if (type.IsDictionaryType())
		{
			foreach (var arg in type.GetGenericArguments()) CollectImports(arg, names);
			return;
		}

		if (type.IsCollectionType())
		{
			CollectImports(type.GetCollectionType(), names);
			return;
		}

		if (type.IsGenericType)
		{
			foreach (var arg in type.GetGenericArguments()) CollectImports(arg, names);
			names.Add(type.Name.RemoveTypeArity());
			return;
		}

		if (type.IsSystemType()) return;
		names.Add(type.Name);
	}

	private static string PageName(Type page)
	{
		var name = page.Name;
		return name.Length > "Model".Length && name.EndsWith("Model", StringComparison.Ordinal)
			? name[..^"Model".Length]
			: name;
	}

	/// <summary>Mirrors the Razor Pages folder structure, derived from the "*.Pages.*" namespace convention.</summary>
	private static string PageDirectory(Type page)
	{
		var ns = page.Namespace ?? string.Empty;
		var index = ns.IndexOf(".Pages", StringComparison.Ordinal);
		if (index < 0) return string.Empty;
		return ns[(index + ".Pages".Length)..].TrimStart('.').Replace('.', '/');
	}

	/// <summary>Generates a global RouteId union of all page routes, for typed hrefs.</summary>
	private static void WriteRouteIds(SvelteOptions options, string generatedTypesDir, string svelteDir)
	{
		var pagesDir = Path.Combine(options.ContentRoot, "Pages");
		var routes = (Directory.Exists(pagesDir)
				? Directory.GetFiles(pagesDir, "*.cshtml", SearchOption.AllDirectories)
				: [])
			.Select(f => "/" + Path.GetRelativePath(pagesDir, f).Replace('\\', '/'))
			.Where(f => !f.Contains("/_"))
			.Select(f => f[..^".cshtml".Length])
			.Select(f => f == "/Index" ? "/" : f.EndsWith("/Index", StringComparison.Ordinal) ? f[..^"/Index".Length] : f)
			.Distinct()
			.Order()
			.Select(f => $"\"{f}\"")
			.ToList();

		File.Delete(Path.Combine(svelteDir, "routes.d.ts"));
		File.WriteAllText(Path.Combine(generatedTypesDir, "routes.d.ts"), $$"""
// Generated by SvelteNet — do not edit.
declare global {
	type RouteId = {{(routes.Count == 0 ? "never" : string.Join(" | ", routes))}} | (string & {});
}
export {};
""" + Environment.NewLine);
	}

	private static void WriteIfMissing(string path, string content)
	{
		if (File.Exists(path)) return;
		File.WriteAllText(path, content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
			? content
			: content + Environment.NewLine);
		Console.WriteLine($"[SvelteNet] Scaffolded {path}");
	}
}
