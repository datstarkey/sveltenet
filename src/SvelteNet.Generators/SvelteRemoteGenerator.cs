namespace SvelteNet.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Emits compiled dispatchers for [SvelteRemote] classes: argument binding and
/// invocation as generated code (no reflection at dispatch time), registered via a
/// module initializer into SvelteRemoteDescriptors. The reflection fallback in
/// SvelteNet.Core covers classes compiled without this generator.
/// </summary>
[Generator]
public sealed class SvelteRemoteGenerator : IIncrementalGenerator
{
	private sealed record ParameterModel(string JsonName, string FullType, bool IsCancellationToken, bool HasDefault, string DefaultLiteral);

	private sealed record MethodModel(string Name, string Kind, string FullReturnType, string ReturnShape, ParameterModel[] Parameters);

	private sealed record ServiceModel(string ServiceName, string FullTypeName, string HintName, MethodModel[] Methods);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var services = context.SyntaxProvider.ForAttributeWithMetadataName(
				"SvelteNet.SvelteRemoteAttribute",
				static (node, _) => node is ClassDeclarationSyntax,
				static (ctx, _) => Collect((INamedTypeSymbol)ctx.TargetSymbol))
			.Where(static m => m is not null);

		context.RegisterSourceOutput(services, static (spc, model) => spc.AddSource(model!.HintName, Emit(model)));
	}

	private static ServiceModel? Collect(INamedTypeSymbol type)
	{
		var methods = new List<MethodModel>();
		foreach (var member in type.GetMembers().OfType<IMethodSymbol>())
		{
			if (member.MethodKind != MethodKind.Ordinary || member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
				continue;

			var kind = KindOf(member);
			if (kind is null) continue;

			var (returnType, shape) = UnwrapReturn(member.ReturnType);
			var parameters = member.Parameters.Select(p => new ParameterModel(
				CamelCase(p.Name),
				p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				p.Type.ToDisplayString() == "System.Threading.CancellationToken",
				p.HasExplicitDefaultValue,
				p.HasExplicitDefaultValue ? DefaultLiteral(p) : "default")).ToArray();

			methods.Add(new MethodModel(member.Name, kind, returnType, shape, parameters));
		}

		if (methods.Count == 0) return null;

		var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		return new ServiceModel(type.Name, fullName, $"{type.Name}_{(uint)fullName.GetHashCode()}.SvelteRemote.g.cs", methods.ToArray());
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
	private static (string FullType, string Shape) UnwrapReturn(ITypeSymbol returnType)
	{
		var display = returnType.ToDisplayString();
		if (display == "void") return ("void", "void");
		if (display is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask") return ("void", "await-void");

		if (returnType is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named &&
		    named.ConstructedFrom.ToDisplayString() is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>")
			return (named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "await-value");

		return (returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "value");
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
		sb.AppendLine($"\tinternal static class {model.ServiceName}RemoteDispatcher_{(uint)model.FullTypeName.GetHashCode()}");
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
				.Select(p => $"new global::SvelteNet.Remote.RemoteParameter(\"{p.JsonName}\", typeof({p.FullType}))"));
			sb.AppendLine($"\t\t\t\t\tnew global::SvelteNet.Remote.RemoteMethodDescriptor(\"{method.Name}\", global::SvelteNet.RemoteKind.{method.Kind},");
			sb.AppendLine($"\t\t\t\t\t\tstatic (service, args) => Invoke_{method.Name}(({model.FullTypeName})service, args),");
			sb.AppendLine($"\t\t\t\t\t\t{returnTypeOf},");
			sb.AppendLine($"\t\t\t\t\t\tnew global::SvelteNet.Remote.RemoteParameter[] {{ {parameters} }}),");
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
}
