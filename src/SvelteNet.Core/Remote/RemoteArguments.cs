namespace SvelteNet.Remote;

using SvelteNet.TypeGen;
using System.Text.Json;

/// <summary>
/// Argument source for a remote call: JSON (query/command) or form fields ([Form]).
/// Get() collects binding failures as per-field errors instead of throwing, so a
/// submission reports every problem at once. Generated dispatchers check CanInvoke
/// after binding and skip the method call when binding failed (or in validate-only mode).
/// Errors use the ValidationProblemDetails shape (field → messages) end to end.
/// </summary>
public sealed class RemoteArguments
{
	public JsonElement? Json { get; init; }
	public IReadOnlyDictionary<string, string>? Form { get; init; }
	public bool ValidateOnly { get; init; }
	public CancellationToken CancellationToken { get; init; }

	/// <summary>
	/// Validation pipeline installed by the host (the registered ISvelteRemoteValidators).
	/// Dispatchers — generated and reflection alike — await ValidateBoundAsync() between
	/// binding and invocation, so validators see every bound value and can veto the call.
	/// </summary>
	public Func<RemoteArguments, ValueTask>? Validation { get; init; }

	/// <summary>Values bound so far, keyed by camelCase parameter name.</summary>
	public Dictionary<string, object?> Bound { get; } = new();

	public Dictionary<string, List<string>>? Errors { get; private set; }

	public bool CanInvoke => Errors is null && !ValidateOnly;

	public bool HasError(string field) => Errors?.ContainsKey(field) == true;

	public ValueTask ValidateBoundAsync() => Validation?.Invoke(this) ?? ValueTask.CompletedTask;

	public void AddError(string field, string message)
	{
		Errors ??= new Dictionary<string, List<string>>();
		if (!Errors.TryGetValue(field, out var list)) Errors[field] = list = [];
		list.Add(message);
	}

	public T? Get<T>(string name) => (T?)Get(name, typeof(T), hasDefault: false, defaultValue: default(T));

	public T? GetOptional<T>(string name, T? defaultValue) => (T?)Get(name, typeof(T), hasDefault: true, defaultValue);

	public object? Get(string name, Type type, bool hasDefault, object? defaultValue)
	{
		try
		{
			if (Form is not null && Form.TryGetValue(name, out var formValue))
				return Bound[name] = ConvertFormValue(formValue, type);

			if (Json?.ValueKind == JsonValueKind.Object && Json.Value.TryGetProperty(name, out var element))
				return Bound[name] = element.Deserialize(type, SvelteJson.Options);
		}
		catch (Exception e) when (e is JsonException or FormatException or ArgumentException or OverflowException)
		{
			AddError(name, $"Invalid value for '{name}'.");
			return defaultValue;
		}

		if (!hasDefault) AddError(name, $"Missing argument '{name}'.");
		else Bound[name] = defaultValue;
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
