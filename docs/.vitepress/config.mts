import { defineConfig } from 'vitepress';

export default defineConfig({
	title: 'SvelteNet',
	description: 'Svelte 5 islands for ASP.NET — typed props, SvelteKit-style forms and remote functions, Jint-based SSR.',
	lang: 'en-US',
	lastUpdated: true,
	markdown: {
		languageAlias: { cshtml: 'razor' },
	},
	themeConfig: {
		nav: [
			{ text: 'Guide', link: '/getting-started' },
			{ text: 'Remote functions', link: '/remote-functions' },
		],
		sidebar: [
			{
				text: 'Guide',
				items: [
					{ text: 'Getting started', link: '/getting-started' },
					{ text: 'Dev mode & HMR', link: '/dev-mode' },
					{ text: 'Forms & enhance()', link: '/forms' },
					{ text: 'MVC controllers', link: '/mvc' },
					{ text: 'Remote functions', link: '/remote-functions' },
					{ text: 'Options', link: '/options' },
				],
			},
		],
		socialLinks: [{ icon: 'github', link: 'https://github.com/datstarkey/sveltenet' }],
		outline: 'deep',
		search: { provider: 'local' },
	},
});
