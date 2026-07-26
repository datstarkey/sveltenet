namespace SvelteNet;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Minimal console shim for code running inside the SSR engine. Output goes to the
/// application's logger under the hosting engine's category — a library has no business
/// writing to the host's stdout. log/info/warn/error map onto log levels; the remaining
/// console members are accepted and ignored so component code that calls them does not
/// blow up a render.
/// </summary>
public class ConsoleWrapper
{
	private readonly ILogger _logger;

	public ConsoleWrapper(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

	public void Log(object? any) => _logger.LogInformation("[svelte-ssr] {Message}", any);
	public void Info(object? any) => _logger.LogInformation("[svelte-ssr] {Message}", any);
	public void Debug(object? any) => _logger.LogDebug("[svelte-ssr] {Message}", any);
	public void Warn(object? any) => _logger.LogWarning("[svelte-ssr] {Message}", any);
	public void Error(object? any) => _logger.LogError("[svelte-ssr] {Message}", any);
	public void Trace(object? any) => _logger.LogTrace("[svelte-ssr] {Message}", any);
	public void Assert(bool condition, string message)
	{
		if (!condition) _logger.LogError("[svelte-ssr] Assertion failed: {Message}", message);
	}

	public void Clear() { }
	public void Count(string label) { }
	public void CountReset(string label) { }
	public void Dir(object? any) => _logger.LogDebug("[svelte-ssr] {Message}", any);
	public void Dirxml(object? any) { }
	public void Group(string label) { }
	public void GroupCollapsed(string label) { }
	public void GroupEnd() { }
	public void Table(object? any) => _logger.LogDebug("[svelte-ssr] {Message}", any);
	public void Time(string label) { }
	public void TimeEnd(string label) { }
	public void TimeLog(string label) { }
	public void Profile(string label) { }
	public void ProfileEnd(string label) { }
}
