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

/** ASP.NET's antiforgery field and header names. */
const ANTIFORGERY_FIELD = '__RequestVerificationToken';
const ANTIFORGERY_HEADER = 'RequestVerificationToken';

/**
 * Progressive enhancement for Razor Pages forms, attachment-style:
 *
 *     <form method="post" {@attach enhance({ token: data.antiforgeryToken, onUpdate: (d) => (data = d) })}>
 *
 * Submits via fetch with an X-SvelteNet header; SveltePage answers with the fresh
 * `data` prop as JSON (following post/redirect/get as a second fetch) and `onUpdate`
 * applies it to the island — no page reload. Validation failures arrive as an
 * RFC 9457 problem details response (400) whose `data` extension still carries the
 * fresh props, with the errors in `data.problem`. Without JS the form posts normally.
 *
 * Antiforgery is handled here: pass `token` (every page's data prop carries
 * `antiforgeryToken`) and it is sent as the RequestVerificationToken header, so forms
 * work under AutoValidateAntiforgeryToken without a hidden input in every form. An
 * existing hidden `__RequestVerificationToken` input is used automatically, so
 * server-rendered forms and asp-antiforgery markup keep working untouched.
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
			// Ignore a re-entrant submit while one is already in flight, so a
			// double-click cannot post the same mutation twice.
			if (form.dataset.sveltenetPending === 'true') return;
			form.dataset.sveltenetPending = 'true';

			const action = event.submitter?.getAttribute('formaction') ?? form.action;
			const body = new FormData(form, event.submitter);
			const headers = { 'x-sveltenet': 'true', ...antiforgeryHeader(form, body, options.token) };
			try {
				let response = await fetch(action, { method: 'POST', body, headers });
				let payload = await readPageResponse(response);
				if (payload.redirect) {
					// The handler succeeded and redirected (PRG) — fetch the fresh data.
					response = await fetch(payload.redirect, { headers: { 'x-sveltenet': 'true' } });
					payload = await readPageResponse(response);
					if (options.reset !== false) form.reset();
				}
				if (payload.data !== undefined) options.onUpdate?.(payload.data);
			} catch (error) {
				if (options.onError) options.onError(error);
				else throw error;
			} finally {
				delete form.dataset.sveltenetPending;
			}
		};
		form.addEventListener('submit', onSubmit);
		return () => form.removeEventListener('submit', onSubmit);
	};
}

/**
 * The token as a header. ASP.NET accepts either the form field or the header; the header
 * is used so the token does not depend on the caller adding a hidden input.
 *
 * Resolved at submit time, not when the attachment is created — the token usually comes
 * from the `data` prop, which enhanced responses replace, so a value captured up front
 * would go stale.
 *
 * @param {HTMLFormElement} form
 * @param {FormData} body
 * @param {string | (() => string | undefined)} [token]
 */
function antiforgeryHeader(form, body, token) {
	const supplied = typeof token === 'function' ? token() : token;
	const value =
		supplied ?? body.get(ANTIFORGERY_FIELD) ?? form.querySelector(`input[name="${ANTIFORGERY_FIELD}"]`)?.value;
	return typeof value === 'string' && value.length > 0 ? { [ANTIFORGERY_HEADER]: value } : {};
}

async function readPageResponse(response) {
	const payload = await response.json().catch(() => undefined);
	// A 400 that still carries `data` is a validation failure: the island re-renders
	// with the errors in data.problem rather than throwing.
	if (response.ok || payload?.data !== undefined) return payload ?? {};

	const reason = payload?.detail ?? payload?.title ?? `status ${response.status}`;
	const error = new Error(`Enhanced form submission failed: ${reason}`);
	error.status = response.status;
	error.problem = payload;
	throw error;
}
