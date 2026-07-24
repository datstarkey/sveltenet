import { render } from 'svelte/server';

/**
 * Renders a SvelteNet island to HTML. Executed inside the .NET host's SSR engine.
 * Async so await-expression component trees (resolved via the in-process fetch
 * bridge) render fully on the server.
 *
 * @param {import('svelte').Component<any>} component
 * @param {Record<string, unknown>} [props]
 * @returns {Promise<{ head: string, body: string }>}
 */
export async function renderComponent(component, props) {
	const result = await render(component, props ? { props } : undefined);
	return { head: result.head, body: result.body };
}
