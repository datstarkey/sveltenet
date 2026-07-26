namespace RemoteFunctions.Features.Admin;

using Microsoft.AspNetCore.Authorization;
using SvelteNet;

public record AuditEntry(string Action, string Actor);

/// <summary>
/// Shows the 64-bit contract: AccountId and Balance cross the wire as JSON strings
/// because a JavaScript number cannot hold them exactly, and the generated TypeScript
/// types them as `string` to match. OpenTickets is a plain int and stays a number.
/// </summary>
public record AccountSummary(long AccountId, decimal Balance, int OpenTickets);

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

	// 2^53 + 1 — the smallest integer a JavaScript number cannot represent. It arrives
	// intact because SvelteJson writes it as a string.
	[Query]
	public AccountSummary GetAccount() => new(9007199254740993L, 1234.56m, 3);

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
