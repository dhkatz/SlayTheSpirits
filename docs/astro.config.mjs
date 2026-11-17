// @ts-check
import { defineConfig } from 'astro/config';
import { satteri } from '@astrojs/markdown-satteri';
import sitemap from '@astrojs/sitemap';
import starlight from '@astrojs/starlight';
import { markdownRefs } from './src/lib/markdown-refs.mjs';
import { branch, repo } from './src/lib/site.mjs';

// GitHub Pages serves the site from https://dhkatz.github.io/SlayTheSpirits/, so the docs workflow sets
// DOCS_BASE=/SlayTheSpirits. Locally (dev and preview) it's served from the root: http://localhost:4321/.
// Links in pages must stay relative (or use asset() in src/lib/data.ts) so they work under either.
const base = process.env.DOCS_BASE ?? '/';

export default defineConfig({
	site: 'https://dhkatz.github.io',
	base,
	markdown: {
		// :card[Down the Hatch], :kw[Tipsy]... become inline references (see src/lib/markdown-refs.mjs).
		processor: satteri({
			mdastPlugins: [markdownRefs({ content: new URL('./src/generated/content.json', import.meta.url), base })],
		}),
	},
	integrations: [
		// Starlight adds a sitemap itself unless there is one; this one leaves out the hover boxes (/tips/...),
		// which are pieces of pages, not pages.
		sitemap({ filter: (page) => !new URL(page).pathname.includes('/tips/') }),
		starlight({
			title: 'Slay the Spirits',
			description: 'A multiplayer drinking game for Slay the Spire 2, and SpireKit, the modding SDK it is built on.',
			social: [{ icon: 'github', label: 'GitHub', href: repo }],
			editLink: { baseUrl: `${repo}/edit/${branch}/docs/` },
			customCss: ['@fontsource/kreon/400.css', '@fontsource/kreon/700.css', './src/styles/custom.css', './src/styles/wiki.css'],
			lastUpdated: true,
			components: { Head: './src/components/Head.astro' },
			// Marks "Cards" as the current sidebar link on a card's page, and so on.
			routeMiddleware: './src/routeData.ts',
			// Notes on wiki entries get Starlight's Markdown extras (asides, heading links) too.
			markdown: { processedDirs: ['./src/content/notes'] },
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
					items: [
						{ slug: 'wiki' },
						{ slug: 'wiki/cards' },
						{ slug: 'wiki/relics' },
						{ slug: 'wiki/potions' },
						{ slug: 'wiki/events' },
						{ slug: 'wiki/keywords' },
					],
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
