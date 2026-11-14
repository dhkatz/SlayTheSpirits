// Data generated from the mod's source by `npm run generate` (see scripts/extract-content.mjs and
// mod/tools/SlayTheSpirits.DocsExport). Never edit the generated files by hand.
import content from '../generated/content.json';
import rules from '../generated/rules.json';
import { createRefs } from './refs.mjs';
import taxonomy from './taxonomy.json';

export { content, rules, taxonomy };

export type Card = (typeof content.cards)[number];
export type Relic = (typeof content.relics)[number];
export type Potion = (typeof content.potions)[number];
export type Term = (typeof content.glossary)[number];
export type Character = (typeof content.characters)[number];
export type Preset = (typeof rules.presets)[number];

/** Inline references and keyword links, for this site's base path (see src/lib/refs.mjs). */
export const refs = createRefs(content, import.meta.env.BASE_URL);

/** A site path (a page or a file under public/) with the site's base path; the site is served from /SlayTheSpirits/ on Pages. */
export const href: (path: string) => string = refs.href;

/** A file under public/. */
export const asset = href;

/** Display names for the engine's enum values. */
export const restSiteLimitNames: Record<string, string> = {
	Off: 'Off',
	OncePerAct: 'Once per Act',
	Unlimited: 'Every Rest Site',
};

export const rarityOrder: readonly string[] = taxonomy.rarities;
export const cardTypeOrder: readonly string[] = taxonomy.cardTypes;
/** Card pool keys in the order the game lists characters. */
export const cardPoolOrder: readonly string[] = Object.values(taxonomy.cardPools).map((pool) => pool.key);

/** Index of a value in an order, unknown values last. */
export const rank = (order: readonly string[], value: string) => {
	const index = order.indexOf(value);
	return index < 0 ? order.length : index;
};

// --- Glossary -------------------------------------------------------------------------------------------------------

const terms = new Map(content.glossary.map((term) => [term.slug, term]));

/** A keyword by its slug ("tipsy", "finish-your-drink", "strength"). */
export function term(slug: string): Term {
	const found = terms.get(slug);
	if (!found) throw new Error(`No keyword "${slug}" (see the glossary in scripts/extract-content.mjs)`);
	return found;
}

/** The drinks, lightest first. */
export const drinks = content.glossary.filter((entry) => entry.kind === 'drink');
