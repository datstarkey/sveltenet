// Transport helpers for remote functions. Kept free of runes so they're testable in Node.

export const REMOTE_BASE = '/_sveltenet/remote';

/** @param {string} path @param {Record<string, unknown>} [args] */
export function queryUrl(path, args) {
	const url = `${REMOTE_BASE}/${path}`;
	return args === undefined ? url : `${url}?args=${encodeURIComponent(JSON.stringify(args))}`;
}

/**
 * Reads a remote response: `{ value }` on success, `{ issues }` on a validation
 * failure (400 with per-field issues); anything else throws.
 * @returns {Promise<{ value?: unknown, issues?: Record<string, { message: string }[]> }>}
 */
export async function readResponse(response, path) {
	if (response.status === 400) {
		const body = await response.json().catch(() => undefined);
		if (body && body.issues) return { issues: body.issues };
		throw new Error(`Remote call '${path}' failed with status 400`);
	}
	if (!response.ok) throw new Error(`Remote call '${path}' failed with status ${response.status}`);
	if (response.status === 204) return { value: undefined };
	return { value: await response.json() };
}
