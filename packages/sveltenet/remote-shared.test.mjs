import assert from 'node:assert/strict';
import { test } from 'node:test';
import { queryUrl, readResponse } from './remote-shared.js';

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

test('readResponse surfaces 400 issues instead of throwing', async () => {
	const issues = { label: [{ message: 'A label is required.' }] };
	assert.deepEqual(await readResponse({ ok: false, status: 400, json: async () => ({ issues }) }, 'p'), { issues });
});

test('readResponse throws on other failures', async () => {
	await assert.rejects(() => readResponse({ ok: false, status: 500, json: async () => ({}) }, 'TodoApi/X'), /TodoApi\/X.*500/);
	await assert.rejects(() => readResponse({ ok: false, status: 400, json: async () => ({}) }, 'p'), /400/);
});
