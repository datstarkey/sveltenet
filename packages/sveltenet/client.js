import { hydrate, mount } from 'svelte';

/**
 * Mounts (or hydrates, when the server rendered it) a SvelteNet island.
 * Called by the inline script the .NET renderer emits.
 *
 * @param {import('svelte').Component<any>} component
 * @param {import('./client.d.ts').MountOptions} options
 */
export function mountComponent(component, options) {
	const { target, props } = options;
	return options.hydrate ? hydrate(component, { target, props }) : mount(component, { target, props });
}

/**
 * Progressive enhancement for Razor Pages forms, attachment-style:
 *
 *     <form method="post" {@attach enhance({ onUpdate: (d: IndexData) => (data = d) })}>
 *
 * Submits via fetch with an X-SvelteNet header; SveltePage answers with the fresh
 * `data` prop as JSON (following post/redirect/get as a second fetch) and `onUpdate`
 * applies it to the island — no page reload. Validation failures arrive as an
 * RFC 9457 problem details response (400) whose `data` extension still carries the
 * fresh props, with the errors in `data.problem`. Without JS the form posts normally.
 *
 * @template TData
 * @param {import('./client.d.ts').EnhanceOptions<TData>} [options]
 * @returns {(form: HTMLFormElement) => () => void}
 */
export function enhance(options = {}) {
	return (form) => {
		/** @param {SubmitEvent} event */
		const onSubmit = async (event) => {
			event.preventDefault();
			const action = event.submitter?.getAttribute('formaction') ?? form.action;
			try {
				let response = await fetch(action, {
					method: 'POST',
					body: new FormData(form, event.submitter),
					headers: { 'x-sveltenet': 'true' },
				});
				let payload = await response.json();
				if (payload.redirect) {
					// The handler succeeded and redirected (PRG) — fetch the fresh data.
					response = await fetch(payload.redirect, { headers: { 'x-sveltenet': 'true' } });
					payload = await response.json();
					if (options.reset !== false) form.reset();
				}
				if (payload.data !== undefined) options.onUpdate?.(payload.data);
			} catch (error) {
				if (options.onError) options.onError(error);
				else throw error;
			}
		};
		form.addEventListener('submit', onSubmit);
		return () => form.removeEventListener('submit', onSubmit);
	};
}
