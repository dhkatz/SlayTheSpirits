// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// GitHub Pages serves the site from https://dhkatz.github.io/SlayTheSpirits/, so the docs workflow sets
// DOCS_BASE=/SlayTheSpirits. Locally (dev and preview) it's served from the root: http://localhost:4321/.
// Links in pages must stay relative (or use asset() in src/lib/data.ts) so they work under either.
const base = process.env.DOCS_BASE ?? '/';

export default defineConfig({
	site: 'https://dhkatz.github.io',
	base,
	integrations: [
		starlight({
			title: 'Slay the Spirits',
			description: 'A multiplayer drinking game for Slay the Spire 2, and SpireKit, the modding SDK it is built on.',
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/dhkatz/SlayTheSpirits' }],
			editLink: { baseUrl: 'https://github.com/dhkatz/SlayTheSpirits/edit/main/docs/' },
			customCss: ['./src/styles/custom.css'],
			lastUpdated: true,
			sidebar: [
				{
					label: 'Play',
					items: [
						{ slug: 'play/getting-started' },
						{ slug: 'play/how-it-works' },
						{ slug: 'play/rule-sets' },
						{ slug: 'play/rest-sites' },
						{ slug: 'play/drink-responsibly' },
					],
				},
				{
					label: 'Wiki',
					items: [{ slug: 'wiki/cards' }, { slug: 'wiki/relics' }, { slug: 'wiki/potions' }],
				},
				{
					label: 'SpireKit',
					items: [{ slug: 'spirekit/overview' }],
				},
				{
					label: 'Development',
					items: [{ slug: 'development/building' }, { slug: 'development/architecture' }],
				},
			],
		}),
	],
});
