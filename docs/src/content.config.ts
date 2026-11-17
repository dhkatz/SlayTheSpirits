import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
	docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
	// Hand-written notes for wiki entries: src/content/notes/<cards|relics|potions|events>/<slug>.md, shown on that entry's
	// page under "Notes". scripts/extract-content.mjs fails the build on a note whose entry doesn't exist.
	notes: defineCollection({ loader: glob({ pattern: '**/*.{md,mdx}', base: './src/content/notes' }) }),
};
