// Markdown directives for inline references, in any .md or .mdx page or note:
//
//   :card[Down the Hatch]   :relic[Hip Flask]   :potion[Tallboy]   :event[Speakeasy]   :kw[Tipsy]   :char[Ironclad]
//
// Each becomes the entry's icon and name, linked to its page, with a hover box (src/lib/refs.mjs). To show other
// words, give a label: :kw[Finish Your Drink]{label="finishing your drink"}. To drop the icon, add {noicon}.
// A name that doesn't exist fails the build, so a renamed card can't leave a dead reference behind.
//
// This is a plugin for Sätteri, Astro's Markdown processor. Starlight already turns directives on (for its asides)
// and puts back any that no plugin handled, so only the names below are touched.

import { readFileSync } from 'node:fs';
import { createRefs, directives } from './refs.mjs';

/** @param {{ content: string | URL, base: string }} options  content: path to the generated content.json */
export function markdownRefs({ content, base }) {
	return ({ sourceFormat }) => {
		// Read for each file, so `npm run generate` during `astro dev` is picked up.
		const refs = createRefs(JSON.parse(readFileSync(content, 'utf8')), base);
		return {
			name: 'slay-the-spirits-refs',
			textDirective(node, ctx) {
				if (!(node.name in directives)) return;
				const name = ctx.textContent(node);
				let html;
				try {
					html = refs.ref(directives[node.name], name, {
						label: node.attributes?.label || undefined,
						icon: !('noicon' in (node.attributes ?? {})),
					});
				} catch (error) {
					throw new Error(`:${node.name}[${name}] ${error.message}`);
				}
				// MDX has no raw HTML nodes; a fragment with set:html is how Starlight's own plugins do it.
				return sourceFormat === 'mdx'
					? { type: 'mdxJsxTextElement', name: 'Fragment', attributes: [{ type: 'mdxJsxAttribute', name: 'set:html', value: html }] }
					: { type: 'html', value: html };
			},
		};
	};
}
