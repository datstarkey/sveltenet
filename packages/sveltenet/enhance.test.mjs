import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Window } from 'happy-dom';
import { enhance } from './client.js';

function setupDom() {
	const window = new Window({ url: 'http://localhost:5219/' });
	globalThis.FormData = window.FormData;

	window.document.body.innerHTML = `
		<form method="post" action="http://localhost:5219/">
			<input name="NewLabel" value="default" />
			<button type="submit">Add</button>
			<button type="submit" id="alt" formaction="http://localhost:5219/?handler=toggle">Toggle</button>
		</form>`;

	const form = window.document.querySelector('form');
	return { window, form };
}

function stubFetch(responses) {
	const calls = [];
	globalThis.fetch = async (url, init) => {
		calls.push({ url: String(url), init });
		return { json: async () => responses.shift() };
	};
	return calls;
}

function submit(window, form, submitter) {
	form.dispatchEvent(new window.SubmitEvent('submit', { cancelable: true, bubbles: true, submitter }));
	return new Promise((resolve) => setTimeout(resolve, 20));
}

test('post/redirect/get flow updates data and resets the form', async () => {
	const { window, form } = setupDom();
	const calls = stubFetch([{ redirect: '/after' }, { data: { title: 'fresh' } }]);
	const updates = [];
	enhance({ onUpdate: (d) => updates.push(d) })(form);

	form.querySelector('input').value = 'typed by user';
	await submit(window, form);

	assert.equal(calls.length, 2);
	assert.equal(calls[0].url, 'http://localhost:5219/');
	assert.equal(calls[0].init.method, 'POST');
	assert.equal(calls[0].init.headers['x-sveltenet'], 'true');
	assert.ok(calls[0].init.body instanceof globalThis.FormData);
	assert.equal(calls[1].url, '/after');
	assert.equal(calls[1].init.headers['x-sveltenet'], 'true');
	assert.deepEqual(updates, [{ title: 'fresh' }]);
	assert.equal(form.querySelector('input').value, 'default', 'form should reset after success');
});

test('validation failure applies the problem data without resetting the form', async () => {
	const { window, form } = setupDom();
	// 400 problem+json: the `data` extension member still carries the fresh props
	const problemData = { problem: { status: 400, errors: { newLabel: ['required'] } } };
	const calls = stubFetch([{ status: 400, errors: { newLabel: ['required'] }, data: problemData }]);
	const updates = [];
	enhance({ onUpdate: (d) => updates.push(d) })(form);

	form.querySelector('input').value = 'typed by user';
	await submit(window, form);

	assert.equal(calls.length, 1);
	assert.deepEqual(updates, [problemData]);
	assert.equal(form.querySelector('input').value, 'typed by user', 'failed submits keep user input');
});

test('reset: false keeps user input after success', async () => {
	const { window, form } = setupDom();
	stubFetch([{ redirect: '/' }, { data: {} }]);
	enhance({ reset: false, onUpdate: () => {} })(form);

	form.querySelector('input').value = 'typed by user';
	await submit(window, form);

	assert.equal(form.querySelector('input').value, 'typed by user');
});

test('the submitter button formaction wins over the form action', async () => {
	const { window, form } = setupDom();
	const calls = stubFetch([{ data: {} }]);
	enhance({ onUpdate: () => {} })(form);

	await submit(window, form, form.querySelector('#alt'));

	assert.equal(calls[0].url, 'http://localhost:5219/?handler=toggle');
});

test('network failures go to onError', async () => {
	const { window, form } = setupDom();
	globalThis.fetch = async () => {
		throw new Error('offline');
	};
	const errors = [];
	enhance({ onError: (e) => errors.push(e) })(form);

	await submit(window, form);

	assert.equal(errors.length, 1);
	assert.equal(errors[0].message, 'offline');
});

test('the returned cleanup detaches the listener', async () => {
	const { window, form } = setupDom();
	const calls = stubFetch([{ data: {} }]);
	const detach = enhance({ onUpdate: () => {} })(form);

	detach();
	await submit(window, form);

	assert.equal(calls.length, 0);
});
