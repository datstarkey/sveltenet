namespace Svelte.Net.Core.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;
public class ViteManifest:   Dictionary<string, ViteRoute>
{
	
}

public class ViteRoute
{
	[JsonPropertyName("file")]
	public string? File { get; set; }
	[JsonPropertyName("src")]
	public string? Src { get; set; }
	[JsonPropertyName("imports")]
	public List<string>? Imports { get; set; }
	[JsonPropertyName("css")]
	public List<string>? Css { get; set; }
}