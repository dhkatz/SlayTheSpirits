// Data generated from the mod's source by `npm run generate` (see scripts/extract-content.mjs and
// mod/tools/SlayTheSpirits.DocsExport). Never edit the generated files by hand.
import content from '../generated/content.json';
import rules from '../generated/rules.json';

export { content, rules };

export type Card = (typeof content.cards)[number];
export type Relic = (typeof content.relics)[number];
export type Potion = (typeof content.potions)[number];
export type Preset = (typeof rules.presets)[number];

/** A path under public/, with the site's base path (the site is served from /SlayTheSpirits/). */
export function asset(path: string): string {
	const base = import.meta.env.BASE_URL.replace(/\/$/, '');
	return `${base}/${path.replace(/^\//, '')}`;
}

/** Display names for the engine's enum values. */
export const restSiteLimitNames: Record<string, string> = {
	Off: 'Off',
	OncePerAct: 'Once per Act',
	Unlimited: 'Every Rest Site',
};

export const rarityOrder = ['Basic', 'Common', 'Uncommon', 'Rare', 'Event', 'Curse', 'Status'];
