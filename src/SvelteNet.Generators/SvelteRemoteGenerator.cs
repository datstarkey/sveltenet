namespace SvelteNet.Generators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Emits compiled dispatchers for [SvelteRemote] classes: argument binding and
/// invocation as generated code (no reflection at dispatch time), registered via a
/// module initializer into SvelteRemoteDescriptors. The reflection fallback in
/// SvelteNet.Core covers classes compiled without this generator.
/// </summary>
[Generator]
public sealed class SvelteRemoteGenerator : IIncrementalGenerator
{
	private sealed record ParameterModel(
		string JsonName,
		string FullType,
		string TypeScriptType,
		bool IsNullable,
		string[] ValidationAttributes,
		string? FluentValidatorServiceType,
		bool IsCancellationToken,
		bool HasDefault,
		string DefaultLiteral);

	private sealed record MethodModel(
		string Name,
		string Kind,
		string FullReturnType,
		string TypeScriptReturnType,
		string ReturnShape,
		ParameterModel[] Parameters);

	private sealed record ServiceModel(
		string ServiceName,
		string FullTypeName,
		string StableId,
		string HintName,
		MethodModel[] Methods,
		string? DuplicateMethod,
		Location? DuplicateLocation);

	private static readonly DiagnosticDescriptor OverloadsNotSupported = new(
		"SVELTENET001",
		"Remote method overloads are not supported",
		"Remote service '{0}' contains overloads named '{1}'. Remote routes are name-based; rename the methods so every remote method has a unique name.",
		"SvelteNet",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var services = context.SyntaxProvider.ForAttributeWithMetadataName(
				"SvelteNet.SvelteRemoteAttribute",
				static (node, _) => node is ClassDeclarationSyntax,
				static (ctx, _) => Collect((INamedTypeSymbol)ctx.TargetSymbol, ctx.SemanticModel.Compilation))
			.Where(static m => m is not null);

		context.RegisterSourceOutput(services, static (spc, model) =>
		{
			if (model!.DuplicateMethod is not null)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					OverloadsNotSupported,
					model.DuplicateLocation,
					model.ServiceName,
					model.DuplicateMethod));
				return;
			}

			spc.AddSource(model.HintName, Emit(model));
		});
	}

	private static ServiceModel? Collect(INamedTypeSymbol type, Compilation compilation)
	{
		var hasFluentValidation = compilation.GetTypeByMetadataName("FluentValidation.IValidator`1") is not null;
		var methods = new List<MethodModel>();
		foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
		{
			if (member.MethodKind != MethodKind.Ordinary || member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
				continue;

			var kind = KindOf(member);
			if (kind is null) continue;

			var (returnTypeSymbol, shape) = UnwrapReturn(member.ReturnType);
			var returnType = returnTypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "void";
			var returnTypeScript = returnTypeSymbol is null
				? "void"
				: SvelteMetadataGenerator.TypeScriptType(
					returnTypeSymbol,
					SvelteMetadataGenerator.IsNullable(returnTypeSymbol, returnTypeSymbol.NullableAnnotation));
			var parameters = member.Parameters.Select(p => new ParameterModel(
				CamelCase(p.Name),
				p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				SvelteMetadataGenerator.TypeScriptType(
					p.Type,
					SvelteMetadataGenerator.IsNullable(p.Type, p.NullableAnnotation)),
				SvelteMetadataGenerator.IsNullable(p.Type, p.NullableAnnotation),
				p.GetAttributes()
					.Where(a => InheritsFrom(a.AttributeClass, "System.ComponentModel.DataAnnotations.ValidationAttribute"))
					.Select(AttributeExpression)
					.ToArray(),
				hasFluentValidation && p.Type.ToDisplayString() != "System.Threading.CancellationToken"
					? $"typeof(global::FluentValidation.IValidator<{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>)"
					: null,
				p.Type.ToDisplayString() == "System.Threading.CancellationToken",
				p.HasExplicitDefaultValue,
				p.HasExplicitDefaultValue ? DefaultLiteral(p) : "default")).ToArray();

			methods.Add(new MethodModel(member.Name, kind, returnType, returnTypeScript, shape, parameters));
		}

		if (methods.Count == 0) return null;

		var duplicate = methods
			.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(g => g.Count() > 1);
		var duplicateSymbol = duplicate is null
			? null
			: type.GetMembers(duplicate.Key).OfType<IMethodSymbol>().Skip(1).FirstOrDefault();
		var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var stableId = StableHash(fullName).ToString("x8");
		return new ServiceModel(
			type.Name,
			fullName,
			stableId,
			$"{type.Name}_{stableId}.SvelteRemote.g.cs",
			methods.ToArray(),
			duplicate?.Key,
			duplicateSymbol?.Locations.FirstOrDefault());
	}

	private static uint StableHash(string value)
	{
		const uint offset = 2166136261;
		const uint prime = 16777619;
		var hash = offset;
		foreach (var c in value)
		{
			hash ^= c;
			hash *= prime;
		}

		return hash;
	}

	private static string? KindOf(IMethodSymbol method)
	{
		foreach (var attribute in method.GetAttributes())
		{
			switch (attribute.AttributeClass?.ToDisplayString())
			{
				case "SvelteNet.QueryAttribute": return "Query";
				case "SvelteNet.CommandAttribute": return "Command";
				case "SvelteNet.FormAttribute": return "Form";
			}
		}

		return null;
	}

	/// <summary>Returns the unwrapped (client-visible) return type and how to invoke/await.</summary>
	private static (ITypeSymbol? Type, string Shape) UnwrapReturn(ITypeSymbol returnType)
	{
		var display = returnType.ToDisplayString();
		if (display == "void") return (null, "void");
		if (display is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask") return (null, "await-void");

		if (returnType is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named &&
			named.ConstructedFrom.ToDisplayString() is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>")
			return (named.TypeArguments[0], "await-value");

		return (returnType, "value");
	}

	private static bool InheritsFrom(INamedTypeSymbol? type, string baseType)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (current.ToDisplayString() == baseType) return true;
		}
		return false;
	}

	private static string AttributeExpression(AttributeData attribute)
	{
		var type = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var constructor = string.Join(", ", attribute.ConstructorArguments.Select(TypedConstantLiteral));
		var named = attribute.NamedArguments.Length == 0
			? string.Empty
			: " { " + string.Join(", ", attribute.NamedArguments.Select(a => $"{a.Key} = {TypedConstantLiteral(a.Value)}")) + " }";
		return $"new {type}({constructor}){named}";
	}

	private static string TypedConstantLiteral(TypedConstant constant)
	{
		if (constant.IsNull) return "null";
		if (constant.Kind == TypedConstantKind.Array)
		{
			var elementType = constant.Type is IArrayTypeSymbol array
				? array.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
				: "object";
			return $"new {elementType}[] {{ {string.Join(", ", constant.Values.Select(TypedConstantLiteral))} }}";
		}
		if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
			return $"typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
		if (constant.Type?.TypeKind == TypeKind.Enum)
			return $"({constant.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){Convert.ToInt64(constant.Value)}";

		return constant.Value switch
		{
			string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
			char c => $"'{c}'",
			bool b => b ? "true" : "false",
			float f => $"{f.ToString(System.Globalization.CultureInfo.InvariantCulture)}f",
			double d => $"{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}d",
			decimal m => $"{m.ToString(System.Globalization.CultureInfo.InvariantCulture)}m",
			_ => Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "null"
		};
	}

	private static string DefaultLiteral(IParameterSymbol parameter)
	{
		var value = parameter.ExplicitDefaultValue;
		return value switch
		{
			null => "default",
			string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
			bool b => b ? "true" : "false",
			char c => $"'{c}'",
			_ when parameter.Type.TypeKind == TypeKind.Enum =>
				$"({parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({Convert.ToInt64(value)})",
			float f => $"{f}f",
			double d => $"{d}d",
			decimal m => $"{m}m",
			_ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default"
		};
	}

	/// <summary>Mirrors JsonNamingPolicy.CamelCase for leading acronyms ("URLValue" → "urlValue").</summary>
	private static string CamelCase(string name)
	{
		if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0])) return name;
		var chars = name.ToCharArray();
		for (var i = 0; i < chars.Length; i++)
		{
			if (i > 0 && i + 1 < chars.Length && !char.IsUpper(chars[i + 1])) break;
			chars[i] = char.ToLowerInvariant(chars[i]);
		}

		return new string(chars);
	}

	private static string Emit(ServiceModel model)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated by SvelteNet.Generators />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine("#pragma warning disable 1998");
		sb.AppendLine("namespace SvelteNet.Generated");
		sb.AppendLine("{");
		sb.AppendLine($"\tinternal static class {model.ServiceName}RemoteDispatcher_{model.StableId}");
		sb.AppendLine("\t{");

		sb.AppendLine("\t\t[global::System.Runtime.CompilerServices.ModuleInitializer]");
		sb.AppendLine("\t\tinternal static void Init()");
		sb.AppendLine("\t\t{");
		sb.AppendLine("\t\t\tglobal::SvelteNet.Remote.SvelteRemoteDescriptors.Register(new global::SvelteNet.Remote.RemoteServiceDescriptor(");
		sb.AppendLine($"\t\t\t\t\"{model.ServiceName}\",");
		sb.AppendLine($"\t\t\t\ttypeof({model.FullTypeName}),");
		sb.AppendLine("\t\t\t\tnew global::SvelteNet.Remote.RemoteMethodDescriptor[]");
		sb.AppendLine("\t\t\t\t{");
		foreach (var method in model.Methods)
		{
			var returnTypeOf = method.FullReturnType == "void" ? "typeof(void)" : $"typeof({method.FullReturnType})";
			var parameters = string.Join(", ", method.Parameters
				.Where(p => !p.IsCancellationToken)
				.Select(p =>
				{
					var attributes = p.ValidationAttributes.Length == 0
						? "null"
						: $"new global::System.ComponentModel.DataAnnotations.ValidationAttribute[] {{ {string.Join(", ", p.ValidationAttributes)} }}";
					return $"new global::SvelteNet.Remote.RemoteParameter(\"{p.JsonName}\", typeof({p.FullType}), " +
						   $"\"{EscapeString(p.TypeScriptType)}\", {(p.IsNullable ? "true" : "false")}, {attributes}, " +
						   $"{p.FluentValidatorServiceType ?? "null"})";
				}));
			sb.AppendLine($"\t\t\t\t\tnew global::SvelteNet.Remote.RemoteMethodDescriptor(\"{method.Name}\", global::SvelteNet.RemoteKind.{method.Kind},");
			sb.AppendLine($"\t\t\t\t\t\tstatic (service, args) => Invoke_{method.Name}(({model.FullTypeName})service, args),");
			sb.AppendLine($"\t\t\t\t\t\t{returnTypeOf},");
			sb.AppendLine($"\t\t\t\t\t\tnew global::SvelteNet.Remote.RemoteParameter[] {{ {parameters} }},");
			sb.AppendLine($"\t\t\t\t\t\t\"{EscapeString(method.TypeScriptReturnType)}\"),");
		}

		sb.AppendLine("\t\t\t\t},");
		sb.AppendLine("\t\t\t\tIsGenerated: true));");
		sb.AppendLine("\t\t}");

		foreach (var method in model.Methods)
		{
			sb.AppendLine();
			sb.AppendLine($"\t\tprivate static async global::System.Threading.Tasks.ValueTask<object?> Invoke_{method.Name}({model.FullTypeName} service, global::SvelteNet.Remote.RemoteArguments args)");
			sb.AppendLine("\t\t{");
			var argNames = new List<string>();
			for (var i = 0; i < method.Parameters.Length; i++)
			{
				var p = method.Parameters[i];
				argNames.Add($"arg{i}");
				if (p.IsCancellationToken)
					sb.AppendLine($"\t\t\tvar arg{i} = args.CancellationToken;");
				else if (p.HasDefault)
					sb.AppendLine($"\t\t\tvar arg{i} = args.GetOptional<{p.FullType}>(\"{p.JsonName}\", {p.DefaultLiteral});");
				else
					sb.AppendLine($"\t\t\tvar arg{i} = args.Get<{p.FullType}>(\"{p.JsonName}\");");
			}

			sb.AppendLine("\t\t\tawait args.ValidateBoundAsync();");
			sb.AppendLine("\t\t\tif (!args.CanInvoke) return null;");
			var call = $"service.{method.Name}({string.Join(", ", argNames.Select(a => a + "!"))})";
			sb.AppendLine(method.ReturnShape switch
			{
				"void" => $"\t\t\t{call}; return null;",
				"await-void" => $"\t\t\tawait {call}; return null;",
				"await-value" => $"\t\t\treturn await {call};",
				_ => $"\t\t\treturn {call};"
			});
			sb.AppendLine("\t\t}");
		}

		sb.AppendLine("\t}");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string EscapeString(string value) =>
		value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
