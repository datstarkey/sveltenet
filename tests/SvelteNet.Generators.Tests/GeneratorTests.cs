namespace SvelteNet.Generators.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SvelteNet.AspNetCore;
using Xunit;

public class GeneratorTests
{
	private static CSharpCompilation Compilation(string source)
	{
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Select(path => MetadataReference.CreateFromFile(path))
			.Append(MetadataReference.CreateFromFile(typeof(SveltePropAttribute).Assembly.Location))
			.Append(MetadataReference.CreateFromFile(typeof(SveltePage).Assembly.Location));
		return CSharpCompilation.Create(
			"GeneratorFixture",
			[CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
	}

	[Fact]
	public void Metadata_is_serializer_aware_and_preserves_nullability()
	{
		const string source = """
			using System.Text.Json.Serialization;
			using SvelteNet;
			using SvelteNet.AspNetCore;

			namespace Fixture.Pages;

			[SvelteComponent("Features/Profile/Index")]
			public sealed class IndexModel : SveltePage
			{
				[SvelteProp] public Payload? Payload { get; set; }
			}

			public sealed class Payload
			{
				[JsonPropertyName("display-name")] public required string Name { get; set; }
				[JsonIgnore] public string Secret { get; set; } = "";
				public byte[] Data { get; set; } = [];
			}
			""";

		var result = Run(new SvelteMetadataGenerator(), Compilation(source));
		var generated = Assert.Single(result.Results).GeneratedSources.Single().SourceText.ToString();

		// Type names are namespace-qualified placeholders here; the scaffolder resolves
		// them once it can see every assembly (see TypeScriptNaming).
		Assert.Contains("\"Fixture_Pages_Payload | null\"", generated);
		Assert.Contains("\"Features/Profile/Index\"", generated);
		Assert.Contains("\\\"display-name\\\": string;", generated);
		Assert.Contains("data: string;", generated);
		Assert.DoesNotContain("secret:", generated);
	}

	[Fact]
	public void Remote_overloads_report_a_clear_diagnostic()
	{
		const string source = """
			using SvelteNet;

			[SvelteRemote]
			public sealed class Api
			{
				[Query] public int Get(int id) => id;
				[Query] public string Get(string id) => id;
			}
			""";

		var result = Run(new SvelteRemoteGenerator(), Compilation(source));

		var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "SVELTENET001");
		Assert.Contains("overloads named 'Get'", diagnostic.GetMessage());
	}

	[Fact]
	public void Remote_output_is_deterministic()
	{
		const string source = """
			using SvelteNet;

			namespace Fixture;

			[SvelteRemote]
			public sealed class Api
			{
				[Command] public void Save(int id) { }
			}
			""";
		var compilation = Compilation(source);

		var first = Run(new SvelteRemoteGenerator(), compilation).Results.Single().GeneratedSources.Single();
		var second = Run(new SvelteRemoteGenerator(), compilation).Results.Single().GeneratedSources.Single();

		Assert.Equal(first.HintName, second.HintName);
		Assert.Equal(first.SourceText.ToString(), second.SourceText.ToString());
	}

	/// <summary>
	/// [Authorize] has to reach the descriptor, because that is the single source both the
	/// HTTP endpoints and the SSR bridge read. If the generator drops it, a protected query
	/// silently becomes public during server rendering.
	/// </summary>
	[Fact]
	public void Authorize_attributes_are_lifted_onto_the_descriptor()
	{
		const string source = """
			using Microsoft.AspNetCore.Authorization;
			using SvelteNet;

			namespace Fixture;

			[SvelteRemote]
			[Authorize(Policy = "admin", Roles = "staff")]
			public sealed class Api
			{
				[Query] public int Secret() => 1;
				[Query] [AllowAnonymous] public int Public() => 2;
				[Query] [Authorize(Policy = "extra")] public int Extra() => 3;
			}
			""";

		var result = Run(new SvelteRemoteGenerator(), Compilation(source));
		var generated = Assert.Single(result.Results).GeneratedSources.Single().SourceText.ToString();

		Assert.Contains("new global::SvelteNet.Remote.RemoteAuthorization(\"admin\", \"staff\", null)", generated);
		Assert.Contains("new global::SvelteNet.Remote.RemoteAuthorization(\"extra\", null, null)", generated);
		// GetServiceStatus-style opt-out: exactly one method carries AllowAnonymous = true.
		Assert.Equal(1, generated.Split('\n').Count(line => line.Trim() == "true),"));
	}

	/// <summary>
	/// AuthorizeAttribute is Inherited = true, and both MVC and the reflection fallback
	/// honour that. If the generator only reads directly-applied attributes, a service
	/// deriving from a secured base silently loses its policy on the generated path —
	/// which is the default path.
	/// </summary>
	[Fact]
	public void Authorize_on_a_base_class_is_inherited()
	{
		const string source = """
			using Microsoft.AspNetCore.Authorization;
			using SvelteNet;

			namespace Fixture;

			[Authorize(Policy = "admin")]
			public abstract class SecuredApiBase { }

			[SvelteRemote]
			public sealed class Api : SecuredApiBase
			{
				[Query] public int Get() => 1;
			}
			""";

		var result = Run(new SvelteRemoteGenerator(), Compilation(source));
		var generated = Assert.Single(result.Results).GeneratedSources.Single().SourceText.ToString();

		Assert.Contains("new global::SvelteNet.Remote.RemoteAuthorization(\"admin\", null, null)", generated);
	}

	/// <summary>
	/// SvelteRemoteGenerator emits no dispatcher for a service with no remote methods, so
	/// the metadata initializer must not call one — otherwise adding [SvelteRemote] before
	/// the first method, or commenting the methods out, fails the consumer's build with
	/// CS0103. A second service is present so metadata is emitted at all.
	/// </summary>
	[Fact]
	public void A_remote_service_with_no_methods_does_not_break_the_build()
	{
		const string source = """
			using SvelteNet;

			namespace Fixture;

			public sealed record Thing(string Name);

			[SvelteRemote]
			public sealed class EmptyApi { }

			[SvelteRemote]
			public sealed class RealApi
			{
				[Query] public Thing Get() => new("x");
			}
			""";

		var generated = Assert.Single(Run(new SvelteMetadataGenerator(), Compilation(source)).Results)
			.GeneratedSources.Single().SourceText.ToString();

		Assert.DoesNotContain("EmptyApiRemoteDispatcher", generated);
		Assert.Contains("RealApiRemoteDispatcher", generated);
	}

	/// <summary>A service with no [Authorize] must stay open — no accidental lockout.</summary>
	[Fact]
	public void Services_without_authorize_emit_no_requirements()
	{
		const string source = """
			using SvelteNet;

			namespace Fixture;

			[SvelteRemote]
			public sealed class Api
			{
				[Query] public int Get() => 1;
			}
			""";

		var result = Run(new SvelteRemoteGenerator(), Compilation(source));
		var generated = Assert.Single(result.Results).GeneratedSources.Single().SourceText.ToString();

		Assert.DoesNotContain("RemoteAuthorization", generated);
	}

	private static GeneratorDriverRunResult Run(IIncrementalGenerator generator, CSharpCompilation compilation)
	{
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGenerators(compilation);
		return driver.GetRunResult();
	}
}
