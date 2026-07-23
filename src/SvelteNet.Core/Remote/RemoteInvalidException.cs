namespace SvelteNet;

/// <summary>
/// Thrown inside a remote function to mark the submission invalid — the SvelteKit
/// `invalid(...)` equivalent. Issues keyed by parameter name surface on that field's
/// issues() in the client form; issues with an empty field name are form-level and
/// appear only in fields.allIssues().
/// </summary>
public sealed class RemoteInvalidException : Exception
{
	public RemoteInvalidException(params (string Field, string Message)[] issues)
		: base(issues.Length > 0 ? issues[0].Message : "Invalid")
	{
		Issues = issues;
	}

	public RemoteInvalidException(string field, string message) : this([(field, message)])
	{
	}

	public IReadOnlyList<(string Field, string Message)> Issues { get; }
}
