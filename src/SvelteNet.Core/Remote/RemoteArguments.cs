namespace SvelteNet.Remote;

using SvelteNet.TypeGen;
using System.Text.Json;

/// <summary>
/// Argument source for a remote call: JSON (query/command) or form fields ([Form]).
/// Get() collects binding failures as per-field issues instead of throwing, so a
/// submission reports every problem at once. Generated dispatchers check CanInvoke
/// after binding and skip the method call when binding failed (or in validate-only mode).
/// </summary>
public sealed class RemoteArguments
{
	public JsonElement? Json { get; init; }
	public IReadOnlyDictionary<string, string>? Form { get; init; }
	public bool ValidateOnly { get; init; }
	public CancellationToken CancellationToken { get; init; }

	public Dictionary<string, List<object>>? Issues { get; private set; }

	public bool CanInvoke => Issues is null && !ValidateOnly;

	public void AddIssue(string field, string message)
	{
		Issues ??= new Dictionary<string, List<object>>();
		if (!Issues.TryGetValue(field, out var list)) Issues[field] = list = [];
		list.Add(new { message });
	}

	public T? Get<T>(string name) => (T?)Get(name, typeof(T), hasDefault: false, defaultValue: default(T));

	public T? GetOptional<T>(string name, T? defaultValue) => (T?)Get(name, typeof(T), hasDefault: true, defaultValue);

	public object? Get(string name, Type type, bool hasDefault, object? defaultValue)
	{
		try
		{
			if (Form is not null && Form.TryGetValue(name, out var formValue))
				return ConvertFormValue(formValue, type);

			if (Json?.ValueKind == JsonValueKind.Object && Json.Value.TryGetProperty(name, out var element))
				return element.Deserialize(type, SvelteJson.Options);
		}
		catch (Exception e) when (e is JsonException or FormatException or ArgumentException or OverflowException)
		{
			AddIssue(name, $"Invalid value for '{name}'.");
			return defaultValue;
		}

		if (!hasDefault) AddIssue(name, $"Missing argument '{name}'.");
		return defaultValue;
	}

	/// <summary>Form fields arrive as strings; convert to the parameter's type.</summary>
	private static object? ConvertFormValue(string value, Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type == typeof(string)) return value;
		if (type == typeof(bool)) return value is "on" or "true" or "True";
		if (type.IsEnum) return Enum.Parse(type, value, ignoreCase: true);

		try
		{
			return JsonSerializer.Deserialize(value, type, SvelteJson.Options);
		}
		catch (JsonException)
		{
			// Not raw JSON — treat as a JSON string (dates, guids, ...)
			return JsonSerializer.Deserialize($"\"{value}\"", type, SvelteJson.Options);
		}
	}
}
