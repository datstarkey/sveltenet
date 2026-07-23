namespace SvelteNet;

using System.Text.Json.Serialization;

public class ViteManifest : Dictionary<string, ViteChunk>
{
}

public class ViteChunk
{
	[JsonPropertyName("file")]
	public string? File { get; set; }

	[JsonPropertyName("src")]
	public string? Src { get; set; }

	[JsonPropertyName("isEntry")]
	public bool IsEntry { get; set; }

	[JsonPropertyName("imports")]
	public List<string>? Imports { get; set; }

	[JsonPropertyName("css")]
	public List<string>? Css { get; set; }
}
