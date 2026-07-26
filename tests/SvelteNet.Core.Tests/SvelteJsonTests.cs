namespace SvelteNet.Core.Tests;

using System.Text.Json;
using SvelteNet.TypeGen;

/// <summary>
/// The serialization contract and the TypeScript generator must describe the same bytes.
/// These tests pin the wire format and then assert the generated TS agrees with it.
/// </summary>
public class SvelteJsonTests
{
	private sealed class Numbers
	{
		public long Id { get; set; }
		public ulong Big { get; set; }
		public decimal Price { get; set; }
		public int Count { get; set; }
		public double Ratio { get; set; }
		public long? Optional { get; set; }
	}

	private enum Tier
	{
		Free,
		ProPlus
	}

	private sealed class Keyed
	{
		public Dictionary<Tier, int> ByTier { get; set; } = [];
		public Dictionary<long, string> ById { get; set; } = [];
	}

	/// <summary>
	/// 9007199254740993 is 2^53 + 1 — the smallest integer a JavaScript number cannot
	/// represent. As a JSON number it silently becomes 9007199254740992 in the browser.
	/// </summary>
	[Fact]
	public void Large_integers_and_decimals_are_written_as_strings()
	{
		var json = JsonSerializer.Serialize(
			new Numbers
			{
				Id = 9007199254740993,
				Big = 18446744073709551615,
				Price = 19.99m,
				Count = 42,
				Ratio = 0.5,
				Optional = null
			},
			SvelteJson.Options);

		Assert.Contains("\"id\":\"9007199254740993\"", json);
		Assert.Contains("\"big\":\"18446744073709551615\"", json);
		Assert.Contains("\"price\":\"19.99\"", json);
		// 32-bit and floating point stay numbers — they round-trip through a JS number.
		Assert.Contains("\"count\":42", json);
		Assert.Contains("\"ratio\":0.5", json);
		Assert.Contains("\"optional\":null", json);
	}

	[Fact]
	public void Large_numbers_round_trip_without_loss()
	{
		var original = new Numbers { Id = 9007199254740993, Big = ulong.MaxValue, Price = 0.1m + 0.2m, Count = 7 };

		var round = JsonSerializer.Deserialize<Numbers>(
			JsonSerializer.Serialize(original, SvelteJson.Options),
			SvelteJson.Options)!;

		Assert.Equal(original.Id, round.Id);
		Assert.Equal(original.Big, round.Big);
		Assert.Equal(original.Price, round.Price);
		Assert.Equal(original.Count, round.Count);
	}

	/// <summary>Payloads from other clients still send JSON numbers; those must bind.</summary>
	[Fact]
	public void Numeric_json_is_still_accepted_on_read()
	{
		var value = JsonSerializer.Deserialize<Numbers>(
			"""{"id":123,"big":456,"price":7.5,"count":1,"ratio":0.25}""",
			SvelteJson.Options)!;

		Assert.Equal(123, value.Id);
		Assert.Equal(456ul, value.Big);
		Assert.Equal(7.5m, value.Price);
	}

	[Fact]
	public void Enum_and_large_integer_dictionary_keys_serialize()
	{
		var json = JsonSerializer.Serialize(
			new Keyed
			{
				ByTier = new Dictionary<Tier, int> { [Tier.ProPlus] = 3 },
				ById = new Dictionary<long, string> { [9007199254740993] = "big" }
			},
			SvelteJson.Options);

		Assert.Contains("\"proPlus\":3", json);
		Assert.Contains("\"9007199254740993\":\"big\"", json);
	}

	/// <summary>
	/// The generated declarations have to match the bytes above, or the TypeScript is a
	/// lie: `id: number` on a value that arrives as a string is a runtime bug the compiler
	/// would happily approve.
	/// </summary>
	[Fact]
	public void Generated_typescript_matches_the_wire_format()
	{
		var declaration = typeof(Numbers).GetTypescriptInterface();

		Assert.Contains("id: string;", declaration);
		Assert.Contains("big: string;", declaration);
		Assert.Contains("price: string;", declaration);
		Assert.Contains("count: number;", declaration);
		Assert.Contains("ratio: number;", declaration);
	}

	/// <summary>Enum-keyed dictionaries serialize, so they must generate rather than throw.</summary>
	[Fact]
	public void Enum_keyed_dictionaries_generate_a_partial_record()
	{
		var names = TypeScriptNaming.Resolve([TypeScriptNaming.Placeholder(typeof(Tier))]);
		Assert.Equal(
			"Partial<Record<Tier, number>>",
			TypeScriptNaming.Substitute(typeof(Dictionary<Tier, int>).TsType(), names));
	}

	[Fact]
	public void Large_integer_dictionary_keys_are_typed_as_strings()
	{
		Assert.Equal("{ [key: string]: string; }", typeof(Dictionary<long, string>).TsType());
	}
}
