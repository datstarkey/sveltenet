namespace SvelteNet;

/// <summary>
/// Minimal console shim for code running inside the SSR engine.
/// log/info/warn/error are forwarded to the host console; the rest are no-ops.
/// </summary>
public class ConsoleWrapper
{
	public void Log(object? any) => Console.WriteLine($"[svelte-ssr] {any}");
	public void Info(object? any) => Console.WriteLine($"[svelte-ssr] {any}");
	public void Debug(object? any) { }
	public void Warn(object? any) => Console.WriteLine($"[svelte-ssr] WARN {any}");
	public void Error(object? any) => Console.Error.WriteLine($"[svelte-ssr] ERROR {any}");
	public void Trace(object? any) { }
	public void Assert(bool condition, string message) { }
	public void Clear() { }
	public void Count(string label) { }
	public void CountReset(string label) { }
	public void Dir(object? any) { }
	public void Dirxml(object? any) { }
	public void Group(string label) { }
	public void GroupCollapsed(string label) { }
	public void GroupEnd() { }
	public void Table(object? any) { }
	public void Time(string label) { }
	public void TimeEnd(string label) { }
	public void TimeLog(string label) { }
	public void Profile(string label) { }
	public void ProfileEnd(string label) { }
}
