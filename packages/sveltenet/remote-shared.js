// Transport helpers for remote functions. Kept free of runes so they're testable in Node.

export const REMOTE_BASE = '/_sveltenet/remote';

/** @param {string} path @param {Record<string, unknown>} [args] */
export function queryUrl(path, args) {
	const url = `${REMOTE_BASE}/${path}`;
	return args === undefined ? url : `${url}?args=${encodeURIComponent(JSON.stringify(args))}`;
}

/**
 * Converts an RFC 9457 validation problem's `errors` member (field → messages,
 * the ASP.NET ValidationProblemDetails shape) into the per-field issue objects
 * the form/query API exposes via issues().
 * @param {Record<string, string[]>} errors
 * @returns {Record<string, { message: string }[]>}
 */
export function issuesFromProblem(errors) {
	/** @type {Record<string, { message: string }[]>} */
	const issues = {};
	for (const [field, messages] of Object.entries(errors)) {
		issues[field] = messages.map((message) => ({ message }));
	}
	return issues;
}

/**
 * Reads a remote response: `{ value }` on success, `{ issues }` when the server
 * answered with an RFC 9457 problem details carrying a validation `errors` member
 * (400, application/problem+json); any other failure throws with the problem's
 * detail/title when one was provided.
 * @returns {Promise<{ value?: unknown, issues?: Record<string, { message: string }[]> }>}
 */
export async function readResponse(response, path) {
	if (!response.ok) {
		const problem = await response.json().catch(() => undefined);
		if (problem && problem.errors) return { issues: issuesFromProblem(problem.errors) };
		const reason = problem?.detail ?? problem?.title ?? `status ${response.status}`;
		throw new Error(`Remote call '${path}' failed: ${reason}`);
	}
	if (response.status === 204) return { value: undefined };
	return { value: await response.json() };
}
