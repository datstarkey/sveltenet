import { render } from 'svelte/server';

/**
 * Renders a SvelteNet island to HTML. Executed inside the .NET host's SSR engine.
 *
 * @param {import('svelte').Component<any>} component
 * @param {Record<string, unknown>} [props]
 * @returns {{ head: string, body: string }}
 */
export function renderComponent(component, props) {
	const { head, body } = render(component, props ? { props } : undefined);
	return { head, body };
}
