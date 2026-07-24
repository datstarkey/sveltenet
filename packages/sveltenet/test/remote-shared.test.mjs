import assert from 'node:assert/strict';
import { test } from 'node:test';
import { issuesFromProblem, queryUrl, readResponse } from '../src/remote-shared.js';

test('queryUrl encodes args as a JSON query parameter', () => {
	assert.equal(queryUrl('TodoApi/GetTodos'), '/_sveltenet/remote/TodoApi/GetTodos');
	assert.equal(
		queryUrl('BlogApi/GetPost', { slug: 'a b' }),
		'/_sveltenet/remote/BlogApi/GetPost?args=' + encodeURIComponent('{"slug":"a b"}'),
	);
});

test('readResponse returns values, undefined for 204', async () => {
	assert.deepEqual(await readResponse({ ok: true, status: 200, json: async () => [1] }, 'p'), { value: [1] });
	assert.deepEqual(await readResponse({ ok: true, status: 204, json: async () => assert.fail() }, 'p'), {
		value: undefined,
	});
});

test('readResponse maps a validation problem (RFC 9457 errors member) to issues', async () => {
	const problem = {
		type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
		title: 'One or more validation errors occurred.',
		status: 400,
		errors: { label: ['A label is required.', 'Too short.'] },
	};
	assert.deepEqual(await readResponse({ ok: false, status: 400, json: async () => problem }, 'p'), {
		issues: { label: [{ message: 'A label is required.' }, { message: 'Too short.' }] },
	});
});

test('issuesFromProblem converts field messages to issue objects', () => {
	assert.deepEqual(issuesFromProblem({ a: ['x'], b: ['y', 'z'] }), {
		a: [{ message: 'x' }],
		b: [{ message: 'y' }, { message: 'z' }],
	});
});

test('readResponse throws with the problem detail on non-validation failures', async () => {
	await assert.rejects(
		() => readResponse({ ok: false, status: 500, json: async () => ({ detail: 'boom' }) }, 'TodoApi/X'),
		/TodoApi\/X.*boom/,
	);
	await assert.rejects(() => readResponse({ ok: false, status: 400, json: async () => ({}) }, 'p'), /status 400/);
	await assert.rejects(
		() =>
			readResponse(
				{
					ok: false,
					status: 400,
					json: async () => {
						throw new Error('not json');
					},
				},
				'p',
			),
		/status 400/,
	);
});
