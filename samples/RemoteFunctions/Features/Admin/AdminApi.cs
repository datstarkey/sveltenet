namespace RemoteFunctions.Features.Admin;

using Microsoft.AspNetCore.Authorization;
using SvelteNet;

public record AuditEntry(string Action, string Actor);

/// <summary>
/// Authorization demo. [Authorize] on the class protects every remote function in it;
/// [AllowAnonymous] on a method opts that one back out. The attribute is lifted onto the
/// generated descriptor, so the same policy is enforced whether the call arrives over
/// HTTP or is dispatched by the SSR bridge during a server render — an anonymous page
/// view cannot get protected data baked into its HTML.
/// </summary>
[SvelteRemote]
[Authorize(Policy = "admin")]
public class AdminApi
{
	private static readonly AuditEntry[] Entries =
	[
		new("deleted todo #3", "alice"),
		new("changed retention policy", "bob")
	];

	[Query]
	public IReadOnlyList<AuditEntry> GetAuditLog() => Entries;

	/// <summary>Readable by anyone, even though the service as a whole requires the policy.</summary>
	[Query]
	[AllowAnonymous]
	public string GetServiceStatus() => "ok";

	[Command]
	public void PurgeAuditLog()
	{
		// Demo only — nothing to purge.
	}
}
