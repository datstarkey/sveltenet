namespace SvelteNet;

/// <summary>
/// Exposes a class's public methods as remote functions: callable from Svelte through
/// the generated typed client (tRPC-style) at /_sveltenet/remote/{Class}/{Method}.
/// Attributed classes are registered in DI as scoped services and discovered by the
/// dev-time scaffolder, which generates the typed TypeScript client.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SvelteRemoteAttribute : Attribute
{
}
