import { untrack } from 'svelte';
import { expect, test, vi } from 'vitest';
import { query } from '../src/remote.svelte.js';

function deferred() {
	let resolve;
	const promise = new Promise((done) => {
		resolve = done;
	});
	return { promise, resolve };
}

test('a slower obsolete request cannot overwrite a newer refresh', async () => {
	const first = deferred();
	const second = deferred();
	globalThis.fetch = vi.fn()
		.mockReturnValueOnce(first.promise)
		.mockReturnValueOnce(second.promise);
	const getValue = query(`Race/${crypto.randomUUID()}`);
	const value = getValue();

	const refresh = value.refresh();
	second.resolve(new Response(JSON.stringify('new'), { status: 200 }));
	await refresh;
	first.resolve(new Response(JSON.stringify('old'), { status: 200 }));
	await new Promise((resolve) => setTimeout(resolve, 0));

	expect(untrack(() => value.current)).toBe('new');
	expect(untrack(() => value.loading)).toBe(false);
});

test('queries remain deduplicated while a consumer holds them', () => {
	globalThis.fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify('value'), { status: 200 }));
	const getValue = query(`Cache/${crypto.randomUUID()}`);

	expect(getValue()).toBe(getValue());
});
