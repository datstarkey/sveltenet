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

		Assert.Contains("\"Payload | null\"", generated);
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

	private static GeneratorDriverRunResult Run(IIncrementalGenerator generator, CSharpCompilation compilation)
	{
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGenerators(compilation);
		return driver.GetRunResult();
	}
}
