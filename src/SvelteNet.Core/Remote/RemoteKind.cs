namespace SvelteNet;

/// <summary>
/// The flavor of a remote function, mirroring SvelteKit's remote functions.
/// Queries read data over GET; commands write over POST JSON; forms write over
/// form POSTs and degrade gracefully without JavaScript.
/// </summary>
public enum RemoteKind
{
	Query,
	Command,
	Form
}

/// <summary>Marks a [SvelteRemote] method as a query — a cacheable, refreshable read (GET).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class QueryAttribute : Attribute
{
}

/// <summary>Marks a [SvelteRemote] method as a command — a write callable from anywhere (POST JSON).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute
{
}

/// <summary>
/// Marks a [SvelteRemote] method as a form handler — parameters bind from the posted
/// form fields by name. The generated client object spreads onto a &lt;form&gt; and
/// progressively enhances it; without JavaScript the browser posts and is redirected back.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FormAttribute : Attribute
{
}
