// SvelteKit-style remote functions backed by [SvelteRemote] C# services.
// Use through the generated `Svelte/remote.ts` — it wires paths and types.
import { hydratable } from 'svelte';
import { createAttachmentKey } from 'svelte/attachments';
import { REMOTE_BASE, queryUrl, readResponse } from './remote-shared.js';

const HEADERS = { 'x-sveltenet': 'true' };
const canFetch = typeof fetch === 'function';
const isServer = typeof window === 'undefined';

/**
 * Queries fetched during SSR are stashed into the page head (devalue-serialized)
 * and reused during hydration instead of re-fetching. Falls back to a plain fetch
 * when hydratable is unavailable (experimental async disabled).
 */
function stash(key, fn) {
	try {
		return hydratable(key, fn);
	} catch (error) {
		console.warn(`[sveltenet] hydratable unavailable for '${key}': ${error?.message ?? error}`);
		return fn();
	}
}

/**
 * For fully server-rendered awaits: a static `{#snippet pending()}` makes the
 * compiler DROP the boundary's children from the server bundle. Passing the
 * snippet through this helper (`<svelte:boundary pending={clientPending(loading)}>`)
 * keeps it client-only, so SSR awaits and renders the real content.
 */
export function clientPending(snippet) {
	return isServer ? undefined : snippet;
}

const activeQueries = new Set();

class RemoteQuery {
	loading = $state(true);
	error = $state(undefined);
	current = $state(undefined);
	// Touched by then(): makes `await getTodos()` re-run after refresh()/set().
	#version = $state(0);
	#path;
	#args;
	#promise;

	constructor(path, args) {
		this.#path = path;
		this.#args = args;
		// hydratable: SSR fetches once (via the in-process bridge), serializes the
		// result into the head, and hydration adopts it without re-fetching.
		this.#promise = canFetch
			? this.#track(stash(`sveltenet:${path}:${JSON.stringify(args ?? null)}`, () => Promise.resolve(this.#request())))
			: new Promise(() => {});
		this.#promise.catch(() => {});
	}

	async #request() {
		const response = await fetch(queryUrl(this.#path, this.#args), { headers: HEADERS });
		const { value, issues } = await readResponse(response, this.#path);
		if (issues) throw Object.assign(new Error(`Query '${this.#path}' was invalid`), { issues });
		return value;
	}

	async #track(promise) {
		this.loading = true;
		try {
			const value = await promise;
			this.current = value;
			this.error = undefined;
			return value;
		} catch (error) {
			this.error = error;
			throw error;
		} finally {
			this.loading = false;
		}
	}

	refresh() {
		// Always a real fetch — the hydration stash is only for the initial value.
		this.#promise = this.#track(this.#request());
		this.#version += 1;
		return this.#promise;
	}

	set(value) {
		this.current = value;
		this.error = undefined;
		this.loading = false;
		this.#promise = Promise.resolve(value);
		this.#version += 1;
	}

	then(onfulfilled, onrejected) {
		void this.#version;
		return this.#promise.then(onfulfilled, onrejected);
	}

	catch(onrejected) {
		return this.#promise.then(undefined, onrejected);
	}

	finally(onfinally) {
		return this.#promise.finally(onfinally);
	}
}

/** Cacheable, refreshable server read: `getPosts() === getPosts()` per args. */
export function query(path, mapArgs) {
	const cache = new Map();
	return (...args) => {
		const argsObject = mapArgs ? mapArgs(...args) : undefined;
		const key = JSON.stringify(argsObject ?? null);
		let instance = cache.get(key);
		if (!instance) {
			instance = new RemoteQuery(path, argsObject);
			cache.set(key, instance);
			activeQueries.add(instance);
		}
		return instance;
	};
}

/** Refreshes every query created on this page (forms do this after a successful submit). */
export function refreshAll() {
	return Promise.all([...activeQueries].map((instance) => instance.refresh().catch(() => undefined)));
}

/** Server write callable from anywhere. `await addLike(id).updates(getLikes(id))` refreshes queries after. */
export function command(path, mapArgs) {
	return (...args) => {
		const promise = (async () => {
			const response = await fetch(`${REMOTE_BASE}/${path}`, {
				method: 'POST',
				headers: { ...HEADERS, 'content-type': 'application/json' },
				body: JSON.stringify(mapArgs ? mapArgs(...args) : {}),
			});
			const { value, issues } = await readResponse(response, path);
			if (issues) throw Object.assign(new Error(`Command '${path}' was invalid`), { issues });
			return value;
		})();
		promise.updates = async (...queries) => {
			const result = await promise;
			await Promise.all(queries.map((instance) => instance.refresh()));
			return result;
		};
		return promise;
	};
}

/**
 * Form handler bound to a [Form] C# method. Spread onto a <form>: works without
 * JavaScript (posts and redirects back) and progressively enhances with fields,
 * issues, pending, result, validate, enhance and for — mirroring SvelteKit.
 */
export function form(path) {
	const instances = new Map();
	const create = () =>
		makeForm(path, (id) => {
			const key = String(id);
			let instance = instances.get(key);
			if (!instance) {
				instance = create();
				instances.set(key, instance);
			}
			return instance;
		});
	return create();
}

function makeForm(path, forLookup) {
	const action = `${REMOTE_BASE}/${path}`;
	let pending = $state(0);
	let result = $state(undefined);
	let issues = $state(undefined);
	let values = $state({});
	let element = null;

	async function post(body, extraHeaders = {}) {
		const response = await fetch(action, { method: 'POST', headers: { ...HEADERS, ...extraHeaders }, body });
		return readResponse(response, path);
	}

	async function submitWith(node, submitter) {
		pending++;
		try {
			const outcome = await post(new FormData(node, submitter));
			if (outcome.issues) {
				issues = outcome.issues;
				return false;
			}
			issues = undefined;
			result = outcome.value;
			await refreshAll();
			return outcome.value === undefined ? true : outcome.value;
		} finally {
			pending--;
		}
	}

	function makeField(name) {
		return {
			as: (type, value) => {
				const attrs = { name };
				if (issues?.[name]) attrs['aria-invalid'] = 'true';
				if (type === 'select' || type === 'select multiple') {
					if (type === 'select multiple') attrs.multiple = true;
					return attrs;
				}
				attrs.type = type;
				const current = values[name];
				if (type === 'checkbox') {
					if (value !== undefined) attrs.value = value;
					attrs.checked = value !== undefined ? current === value : current === true || current === 'on';
				} else if (type === 'radio' || type === 'submit') {
					attrs.value = value;
					if (type === 'radio') attrs.checked = current === value;
				} else if (current !== undefined || value !== undefined) {
					attrs.value = current ?? value;
				}
				return attrs;
			},
			issues: () => issues?.[name],
			value: () => values[name],
			set: (value) => {
				values = { ...values, [name]: value };
			},
		};
	}

	const fieldCache = new Map();
	const fields = new Proxy(
		{},
		{
			get(_, prop) {
				if (prop === 'value') return () => ({ ...values });
				if (prop === 'set')
					return (next) => {
						values = { ...values, ...next };
					};
				if (prop === 'allIssues') return () => (issues ? Object.values(issues).flat() : undefined);
				if (typeof prop !== 'string') return undefined;
				let field = fieldCache.get(prop);
				if (!field) {
					field = makeField(prop);
					fieldCache.set(prop, field);
				}
				return field;
			},
		},
	);

	const makeSubmission = (node, submitter) =>
		defineHidden(
			{},
			{
				fields,
				element: node,
				submit: () => submitWith(node, submitter),
				get pending() {
					return pending;
				},
				get result() {
					return result;
				},
			},
		);

	function attachment(enhanceCallback) {
		return (node) => {
			element = node;
			const onInput = (event) => {
				const target = event.target;
				if (!target?.name) return;
				const value =
					target.type === 'checkbox' ? (target.checked ? (target.value === 'on' ? true : target.value) : undefined) : target.value;
				values = { ...values, [target.name]: value };
			};
			const onSubmit = async (event) => {
				event.preventDefault();
				if (enhanceCallback) {
					await enhanceCallback(makeSubmission(node, event.submitter));
				} else {
					const outcome = await submitWith(node, event.submitter);
					if (outcome !== false) {
						node.reset();
						values = {};
					}
				}
			};
			node.addEventListener('input', onInput);
			node.addEventListener('submit', onSubmit);
			return () => {
				node.removeEventListener('input', onInput);
				node.removeEventListener('submit', onSubmit);
			};
		};
	}

	// Only method, action, and the attachment spread onto the <form> element.
	const api = { method: 'POST', action, [createAttachmentKey()]: attachment() };
	return defineHidden(api, {
		fields,
		validate: async () => {
			if (!element) return;
			const outcome = await post(new FormData(element), { 'x-sveltenet-validate': 'true' });
			issues = outcome.issues;
		},
		for: (id) => forLookup(id),
		enhance: (callback) => ({ method: 'POST', action, [createAttachmentKey()]: attachment(callback) }),
		get pending() {
			return pending;
		},
		get result() {
			return result;
		},
	});
}

function defineHidden(target, members) {
	for (const [key, value] of Object.entries(Object.getOwnPropertyDescriptors(members))) {
		Object.defineProperty(target, key, { ...value, enumerable: false });
	}
	return target;
}
