namespace Svelte.Net.Core.Extensions;

using System;

public static class StringExtensions
{
	public static string ToCamelCase(this string str) =>
		string.IsNullOrEmpty(str) || str.Length < 2
			? str.ToLowerInvariant()
			: char.ToLowerInvariant(str[0]) + str.Substring(1);
}
