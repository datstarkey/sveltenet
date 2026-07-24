import { playwright } from '@vitest/browser-playwright';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [svelte({ compilerOptions: { experimental: { async: true } } })],
	test: {
		include: ['test/*.svelte.spec.js'],
		browser: {
			enabled: true,
			provider: playwright(),
			instances: [{ browser: 'chromium' }],
		},
	},
});
