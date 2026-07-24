namespace SvelteNet;

/// <summary>
/// Thrown inside a remote function (or any SvelteNet handler) to mark the submission
/// invalid — the SvelteKit `invalid(...)` equivalent. Errors are keyed by field name
/// and surface to HTTP as an RFC 9457 problem details response with the standard
/// ASP.NET `errors` member; the client maps them onto each field's issues(). Errors
/// with an empty field name are form-level and appear only in fields.allIssues().
/// </summary>
public sealed class SvelteValidationException : Exception
{
	public SvelteValidationException(params (string Field, string Message)[] errors)
		: base(errors.Length > 0 ? errors[0].Message : "One or more validation errors occurred.")
	{
		var dictionary = new Dictionary<string, string[]>();
		foreach (var group in errors.GroupBy(e => e.Field))
			dictionary[group.Key] = group.Select(e => e.Message).ToArray();
		Errors = dictionary;
	}

	public SvelteValidationException(string field, string message) : this([(field, message)])
	{
	}

	public SvelteValidationException(IDictionary<string, string[]> errors)
		: base("One or more validation errors occurred.")
	{
		Errors = new Dictionary<string, string[]>(errors);
	}

	/// <summary>Field name → messages, the shape of ValidationProblemDetails.Errors.</summary>
	public IReadOnlyDictionary<string, string[]> Errors { get; }
}
