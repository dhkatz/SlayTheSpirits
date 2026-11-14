// Inline references: a card, relic, potion, keyword or character mentioned in text, drawn as its icon and name,
// linked to its page, with a hover box (the wiki's equivalent of the community wiki's {{C2}}, {{R2}} and {{KW2}}).
//
// Plain JavaScript with no Astro imports, because three things use it: components (through src/lib/data.ts), the
// Markdown directives (src/lib/markdown-refs.mjs, which runs in astro.config.mjs) and nothing else should build one of
// these by hand.

/** @typedef {'card' | 'relic' | 'potion' | 'keyword' | 'character'} RefKind */

/** The names of the inline directives (:card[...], :kw[...]) and the kind of reference each makes. */
export const directives = { card: 'card', relic: 'relic', potion: 'potion', kw: 'keyword', char: 'character' };

const escapeHtml = (value) => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/**
 * @param {any} content  src/generated/content.json
 * @param {string} base  the site's base path ("/" or "/SlayTheSpirits")
 */
export function createRefs(content, base) {
	const root = base.replace(/\/$/, '');
	/** A site path (a page, or a file under public/) with the base path. */
	const href = (path) => `${root}/${path.replace(/^\//, '')}`;

	/** Where each kind's entries are, and what makes one's link, hover box and icon. */
	const kinds = {
		card: { list: content.cards, page: (c) => `wiki/cards/${c.slug}/`, tip: (c) => `cards/${c.slug}`, icon: (c) => c.look?.icon },
		relic: { list: content.relics, page: (r) => `wiki/relics/${r.slug}/`, tip: (r) => `relics/${r.slug}`, icon: (r) => r.image },
		potion: { list: content.potions, page: (p) => `wiki/potions/${p.slug}/`, tip: (p) => `potions/${p.slug}`, icon: (p) => p.image },
		// The mod's keywords have a page here; the game's link to the community wiki, if it has one for them.
		keyword: {
			list: content.glossary,
			page: (k) => (k.local ? `wiki/keywords/${k.slug}/` : null),
			external: (k) => (k.local ? null : k.wiki),
			tip: (k) => `keywords/${k.slug}`,
			icon: (k) => k.icon,
		},
		character: { list: content.characters, page: () => null, external: (c) => c.wiki, tip: () => null, icon: (c) => c.icon, label: (c) => c.name },
	};

	const normal = (value) => String(value).trim().toLowerCase();

	/** An entry by its slug, id, key or name, in any case. Throws (failing the build) if there isn't one. */
	function find(kind, key) {
		const info = kinds[kind];
		if (!info) throw new Error(`Unknown kind of reference "${kind}" (use ${Object.keys(kinds).join(', ')})`);
		const wanted = normal(key);
		const found = info.list.find((entry) => [entry.slug, entry.id, entry.key, entry.title, entry.name].some((v) => v !== undefined && normal(v) === wanted));
		if (!found) throw new Error(`No ${kind} called "${key}". The ${kind}s are: ${info.list.map((entry) => entry.title ?? entry.name).join(', ')}`);
		return found;
	}

	/**
	 * A reference as a HAST element (what Markdown plugins need).
	 * @param {RefKind} kind
	 * @param {string} key  slug, id or name
	 * @param {{ label?: string, icon?: boolean, link?: boolean, tip?: boolean, className?: string }} [options]
	 *   link: false draws a span with the hover box only, for inside another link.
	 *   tip: false leaves the hover box out, for an entry named on its own page.
	 */
	function node(kind, key, { label, icon = true, link = true, tip: withTip = true, className = '' } = {}) {
		const info = kinds[kind];
		const entry = find(kind, key);
		const page = info.page(entry);
		const target = link ? (page ? href(page) : (info.external?.(entry) ?? null)) : null;
		const tip = withTip ? info.tip(entry) : null;
		const image = icon ? info.icon(entry) : null;
		return {
			type: 'element',
			tagName: target ? 'a' : 'span',
			properties: {
				className: ['sts-ref', `sts-ref--${kind}`, ...(className ? [className] : [])],
				...(target ? { href: target } : {}),
				...(target && !page ? { rel: 'external' } : {}),
				...(tip ? { 'data-tip': tip } : {}),
			},
			children: [
				...(image ? [{ type: 'element', tagName: 'img', properties: { className: ['sts-ref__icon'], src: href(image), alt: '', loading: 'lazy' }, children: [] }] : []),
				{ type: 'text', value: label ?? info.label?.(entry) ?? entry.title },
			],
		};
	}

	const serialize = (element) =>
		element.type === 'text'
			? escapeHtml(element.value)
			: `<${element.tagName}${Object.entries(element.properties)
					.map(([name, value]) => ` ${name === 'className' ? 'class' : name}="${escapeHtml(Array.isArray(value) ? value.join(' ') : value)}"`)
					.join('')}>${element.children.map(serialize).join('')}${element.tagName === 'img' ? '' : `</${element.tagName}>`}`;

	/** A reference as HTML. Same arguments as node(). */
	const ref = (kind, key, options) => serialize(node(kind, key, options));

	/**
	 * Turns the keyword markers in generated text (<span class="sts-gold" data-term="tipsy">Tipsy</span>, written by
	 * scripts/extract-content.mjs) into references. "link": icon, link and hover box, for prose. "hover": the same
	 * words with only the hover box, for card faces and for text inside a link.
	 */
	const terms = (html, mode = 'link') =>
		html.replace(/<span class="sts-gold" data-term="([^"]+)">([^<]*)<\/span>/g, (_, slug, words) => {
			const label = words.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>');
			return ref('keyword', slug, { label, className: 'sts-gold', icon: mode === 'link', link: mode === 'link' });
		});

	/**
	 * One line of text with the same shorthand as the Markdown pages, as HTML: the directives (:kw[Shot],
	 * :char[Ironclad]{label="..."}), [links](wiki/cards/) (site paths get the base path; full URLs are left alone)
	 * and **bold**. For sentences kept in data, like where a pool's cards are found (src/lib/taxonomy.json).
	 */
	function inline(text) {
		const pattern = /:(\w+)\[([^\]]+)\](?:\{([^}]*)\})?|\[([^\]]+)\]\(([^)\s]+)\)|\*\*([^*]+)\*\*/g;
		let html = '';
		let last = 0;
		for (const match of text.matchAll(pattern)) {
			const [whole, directive, name, attributes, linkText, target, strong] = match;
			html += escapeHtml(text.slice(last, match.index));
			last = match.index + whole.length;
			if (directive && directive in directives) {
				html += ref(directives[directive], name, { label: attributes?.match(/label="([^"]*)"/)?.[1], icon: !/\bnoicon\b/.test(attributes ?? '') });
			} else if (linkText) {
				html += `<a href="${escapeHtml(/^[a-z]+:/.test(target) ? target : href(target))}">${escapeHtml(linkText)}</a>`;
			} else if (strong) {
				html += `<strong>${escapeHtml(strong)}</strong>`;
			} else {
				html += escapeHtml(whole);
			}
		}
		return html + escapeHtml(text.slice(last));
	}

	return { href, find, node, ref, terms, inline };
}
