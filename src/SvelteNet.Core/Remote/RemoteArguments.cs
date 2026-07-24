namespace SvelteNet.Remote;

using System.Text.Json;
using SvelteNet.TypeGen;

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
	public IReadOnlyDictionary<string, IReadOnlyList<string>>? Form { get; init; }
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

		if (hasDefault) return Bound[name] = defaultValue;
		if (Form is not null && (Nullable.GetUnderlyingType(type) ?? type) == typeof(bool))
			return Bound[name] = false;
		AddError(name, $"Missing argument '{name}'.");
		return defaultValue;
	}

	/// <summary>Form fields arrive as strings; convert to the parameter's type.</summary>
	private static object? ConvertFormValue(IReadOnlyList<string> values, Type type)
	{
		var target = Nullable.GetUnderlyingType(type) ?? type;
		if (target.IsArray)
			return ConvertCollection(values, target, target.GetElementType()!);

		var collection = target.IsGenericType
			? target.GetInterfaces()
				.Append(target)
				.FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() is
					var definition && (definition == typeof(IEnumerable<>) ||
									   definition == typeof(ICollection<>) ||
									   definition == typeof(IReadOnlyCollection<>) ||
									   definition == typeof(IReadOnlyList<>) ||
									   definition == typeof(IList<>) ||
									   definition == typeof(List<>)))
			: null;
		if (collection is not null)
			return ConvertCollection(values, target, collection.GetGenericArguments()[0]);

		if (values.Count != 1)
			throw new FormatException("A scalar form argument must have exactly one value.");
		return ConvertScalar(values[0], target);
	}

	private static object ConvertCollection(IReadOnlyList<string> values, Type targetType, Type elementType)
	{
		var array = Array.CreateInstance(elementType, values.Count);
		for (var i = 0; i < values.Count; i++) array.SetValue(ConvertScalar(values[i], elementType), i);
		if (targetType.IsAssignableFrom(array.GetType())) return array;

		var json = JsonSerializer.Serialize(array, array.GetType(), SvelteJson.Options);
		return JsonSerializer.Deserialize(json, targetType, SvelteJson.Options)
			   ?? throw new FormatException($"Unable to bind form values to '{targetType.Name}'.");
	}

	private static object? ConvertScalar(string value, Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type == typeof(string)) return value;
		if (type == typeof(bool))
			return value.ToLowerInvariant() switch
			{
				"on" or "true" or "1" => true,
				"off" or "false" or "0" => false,
				_ => throw new FormatException("Invalid boolean form value.")
			};
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
