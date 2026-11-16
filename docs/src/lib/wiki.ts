// The wiki's kinds of entry (cards, relics, potions, keywords), described once: their order, opening sentence,
// infobox, navigation groups and how their compendium filters and sorts. Pages and components read these instead of
// knowing about each kind.
import { token, type Dimension, type Toggle } from './compendium';
import {
	cardPoolOrder,
	cardTypeOrder,
	content,
	href,
	rank,
	rarityOrder,
	refs,
	taxonomy,
	term,
	type Card,
	type Potion,
	type Relic,
	type Term,
} from './data';

/** What every entry has. */
export interface Entry {
	id: string;
	slug: string;
	title: string;
	/** Where it's defined in the mod, if it's a class. */
	source?: { path: string; line: number };
	/** Its tooltips: keyword slugs, in the game's order. */
	tips: string[];
	searchText: string;
}

/** A row of an entry's infobox. `html` replaces `value` where it needs an icon or a link. */
export interface Fact {
	label: string;
	value: string;
	href?: string;
	html?: string;
}

/** A heading in a kind's navigation box. `html` replaces `title` where it has an icon or a link. */
export interface Group {
	title: string;
	html?: string;
}

export type KindKey = 'cards' | 'relics' | 'potions' | 'keywords';
export type RefKind = 'card' | 'relic' | 'potion' | 'keyword';

export interface Kind<T extends Entry> {
	key: KindKey;
	/** Its kind of inline reference (src/lib/refs.mjs). */
	ref: RefKind;
	singular: string;
	plural: string;
	/** The kind's compendium page. */
	page: string;
	/** One line about the kind, for the wiki's front page. */
	blurb: string;
	/** Every entry, in the compendium's default order (which is also the order of the previous/next links). */
	items: readonly T[];
	/** One plain sentence, for search engines and link previews. */
	summary: (item: T) => string;
	/** What it is in a few linked words, under its name in the infobox: "Uncommon Colorless Skill". HTML. */
	tagline: (item: T) => string;
	/** The entry page's opening sentence. HTML: everything it names is a link. */
	lead: (item: T) => string;
	/** How to get it, if that can be said in general. HTML. */
	obtain: (item: T) => string | null;
	/** The entry's rules text, as HTML with keyword markers (for lists and hover boxes). */
	text: (item: T) => string;
	facts: (item: T) => Fact[];
	/** The heading an entry goes under in the navigation box at the bottom of each page. */
	group: (item: T) => Group;
	dimensions: readonly Dimension<T>[];
	/** Switches on the compendium (each sets data-t-<key> on it, for CSS). */
	toggles?: Toggle[];
	/** How the compendium lays items out. */
	layout: 'cards' | 'tiles';
}

const byTitle = (a: Entry, b: Entry) => a.title.localeCompare(b.title);
const lower = (s: string) => s.charAt(0).toLowerCase() + s.slice(1);
const article = (word: string) => (/^[aeiou]/i.test(word) ? 'an' : 'a');
const escape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;');
const keyword = (slug: string, label?: string) => refs.ref('keyword', slug, { label });
const link = (path: string, label: string) => `<a href="${href(path)}">${escape(label)}</a>`;

/** A link to a kind's compendium, filtered to one value of one of its dimensions. */
const filtered = (page: string, key: string, value: string, label = value) => link(`${page}?${key}=${token(value)}`, label);
const filteredHref = (page: string, key: string, value: string) => href(`${page}?${key}=${token(value)}`);

/** An entry named on its own page: its icon and name in bold, with no link or hover box. */
const self = (kind: RefKind, item: Entry) => `<strong>${refs.ref(kind, item.slug, { link: false, tip: false })}</strong>`;

/** The drinks an entry hands out: its tooltips that are drinks, lightest first. */
const drinksOf = (item: Entry) => item.tips.filter((slug) => term(slug).kind === 'drink');
const drinkDimension = <T extends Entry>(): Dimension<T> => ({
	key: 'drink',
	label: 'Drink',
	facet: drinksOf,
	order: content.glossary.map((t) => t.slug),
	display: (slug) => term(slug).title,
	icon: (slug) => term(slug).icon,
});

const rarityDimension = <T extends { rarity: string }>(): Dimension<T> => ({
	key: 'rarity',
	label: 'Rarity',
	facet: (item) => item.rarity,
	order: rarityOrder,
	sort: (item) => rank(rarityOrder, item.rarity),
});

const nameDimension = <T extends Entry>(): Dimension<T> => ({ key: 'name', label: 'Name', sort: (item) => item.title });

// --- Cards ----------------------------------------------------------------------------------------------------------

const pools = new Map(Object.values(taxonomy.cardPools).map((pool) => [pool.key, pool]));
const characters = new Map(content.characters.map((character) => [character.key, character]));
const cardsPage = 'wiki/cards/';

/** A card's pool as a link to the cards in it, with the character's icon if it's a character's. */
function poolLink(card: Card) {
	const icon = card.character ? characters.get(card.character)?.icon : null;
	const image = icon ? `<img class="sts-ref__icon" src="${href(icon)}" alt="" loading="lazy">` : '';
	return `<a class="sts-ref" href="${filteredHref(cardsPage, 'pool', card.pool)}">${image}${escape(card.poolName)}</a>`;
}

export const cards: Kind<Card> = {
	key: 'cards',
	ref: 'card',
	singular: 'card',
	plural: 'cards',
	page: cardsPage,
	blurb: 'The colorless Tavern set, one card for each character, and a curse.',
	layout: 'cards',
	toggles: [{ key: 'upgraded', label: 'Show upgrades' }],
	items: [...content.cards].sort(
		(a, b) =>
			rank(cardPoolOrder, a.pool) - rank(cardPoolOrder, b.pool) ||
			rank(rarityOrder, a.rarity) - rank(rarityOrder, b.rarity) ||
			(a.cost ?? 99) - (b.cost ?? 99) ||
			byTitle(a, b)
	),
	summary: (card) =>
		`${card.title}: ${article(card.rarity)} ${lower(card.rarity)} ${card.poolName} ${lower(card.type)} from Slay the Spirits. ${card.searchText}`,
	// "Uncommon Colorless Skill": each word once (a curse is Curse in all three).
	tagline: (card) =>
		[
			[card.rarity, filtered(cardsPage, 'rarity', card.rarity)],
			[card.poolName, poolLink(card)],
			[card.type, filtered(cardsPage, 'type', card.type)],
		]
			.filter(([word], index, all) => all.findIndex(([other]) => other === word) === index)
			.map(([, html]) => html)
			.join(' '),
	// "Down the Hatch is a 2 [energy] cost Uncommon Attack card for the [icon] Ironclad."
	lead: (card) => {
		const cost =
			card.cost === null
				? `an ${filtered(cardsPage, 'cost', 'Unplayable', 'unplayable')}`
				: `a ${filtered(cardsPage, 'cost', String(card.cost))} <img class="sts-ref__icon sts-ref__icon--energy" src="${href(card.look.energy ?? '')}" alt="Energy"> cost`;
		const words = [
			card.rarity === card.type ? null : filtered(cardsPage, 'rarity', card.rarity),
			card.character || card.poolName === card.type ? null : poolLink(card),
			filtered(cardsPage, 'type', card.type),
		].filter(Boolean);
		const whose = card.character ? ` for the ${refs.ref('character', card.character)}` : '';
		return `${self('card', card)} is ${cost} ${words.join(' ')} ${link(cardsPage, 'card')}${whose}.`;
	},
	obtain: (card) => (card.found ? refs.inline(card.found) : null),
	text: (card) => card.text.combined,
	facts: (card) => [
		card.character
			? { label: 'Character', value: card.poolName, html: refs.ref('character', card.character) }
			: { label: 'Pool', value: card.poolName, href: filteredHref(cardsPage, 'pool', card.pool) },
		{ label: 'Type', value: card.type, href: filteredHref(cardsPage, 'type', card.type) },
		{ label: 'Rarity', value: card.rarity, href: filteredHref(cardsPage, 'rarity', card.rarity) },
		{
			label: 'Cost',
			value: card.cost === null ? 'Unplayable' : `${card.cost}`,
			href: filteredHref(cardsPage, 'cost', card.cost === null ? 'Unplayable' : String(card.cost)),
		},
		{ label: 'Target', value: card.target },
		...(card.keywords.length
			? [{ label: 'Keywords', value: card.keywords.join(', '), html: card.keywords.map((k) => keyword(k)).join(', ') }]
			: []),
		{
			label: 'Upgrade',
			value: card.upgradable ? card.upgrade.map((change) => `${change.label} ${change.from} → ${change.to}`).join(', ') : "Can't be upgraded",
		},
	],
	group: (card) => ({ title: card.poolName, html: poolLink(card) }),
	dimensions: [
		{
			key: 'pool',
			label: 'Pool',
			facet: (card) => card.pool,
			order: cardPoolOrder,
			display: (key) => pools.get(key)?.name ?? key,
			color: (key) => pools.get(key)?.color,
			icon: (key) => characters.get(key)?.icon,
		},
		{
			key: 'type',
			label: 'Type',
			facet: (card) => card.type,
			order: cardTypeOrder,
			sort: (card) => rank(cardTypeOrder, card.type),
		},
		rarityDimension<Card>(),
		{
			key: 'cost',
			label: 'Cost',
			facet: (card) => (card.cost === null ? 'Unplayable' : String(card.cost)),
			sort: (card) => card.cost ?? 99,
		},
		drinkDimension<Card>(),
		{ key: 'keyword', label: 'Keyword', facet: (card) => card.keywords, order: [...taxonomy.cardKeywords.before, ...taxonomy.cardKeywords.after] },
		nameDimension<Card>(),
	],
};

// --- Relics ---------------------------------------------------------------------------------------------------------

const relicsPage = 'wiki/relics/';

export const relics: Kind<Relic> = {
	key: 'relics',
	ref: 'relic',
	singular: 'relic',
	plural: 'relics',
	page: relicsPage,
	blurb: 'Found only by taking a Shot at a rest site.',
	layout: 'tiles',
	items: [...content.relics].sort((a, b) => rank(rarityOrder, a.rarity) - rank(rarityOrder, b.rarity) || byTitle(a, b)),
	summary: (relic) => `${relic.title}: a ${relic.poolName} relic from Slay the Spirits. ${relic.searchText}`,
	tagline: (relic) => `${escape(relic.poolName ?? '')} ${link(relicsPage, 'relic')}`,
	lead: (relic) => `${self('relic', relic)} is a ${link(relicsPage, `${relic.poolName} relic`)}.`,
	obtain: (relic) => (relic.found ? refs.inline(relic.found) : null),
	text: (relic) => relic.description,
	facts: (relic) => [
		{ label: 'Rarity', value: relic.rarity },
		{ label: 'Set', value: relic.poolName ?? '', href: href(relicsPage) },
	],
	group: (relic) => ({ title: `${relic.poolName} relics` }),
	dimensions: [rarityDimension<Relic>(), drinkDimension<Relic>(), nameDimension<Relic>()],
};

// --- Potions --------------------------------------------------------------------------------------------------------

const potionsPage = 'wiki/potions/';

/** "a Sip" or "2 Sips", linked to the Sip keyword. */
const sipsOf = (potion: Potion) =>
	potion.sips === 1 ? `a ${keyword(potion.drink)}` : `${potion.sips} ${keyword(potion.drink, `${term(potion.drink).title}s`)}`;

export const potions: Kind<Potion> = {
	key: 'potions',
	ref: 'potion',
	singular: 'potion',
	plural: 'potions',
	page: potionsPage,
	blurb: 'Potions you drink for real.',
	layout: 'tiles',
	items: [...content.potions].sort((a, b) => rank(rarityOrder, a.rarity) - rank(rarityOrder, b.rarity) || byTitle(a, b)),
	summary: (potion) => `${potion.title}: ${article(potion.rarity)} ${lower(potion.rarity)} potion from Slay the Spirits. ${potion.searchText}`,
	tagline: (potion) => `${filtered(potionsPage, 'rarity', potion.rarity)} ${link(potionsPage, 'potion')}`,
	lead: (potion) =>
		`${self('potion', potion)} is ${article(potion.rarity)} ${filtered(potionsPage, 'rarity', potion.rarity)} ${link(potionsPage, 'potion')}. Using it means taking ${sipsOf(potion)} for real.`,
	obtain: (potion) => (potion.found ? refs.inline(potion.found) : null),
	text: (potion) => potion.description,
	facts: (potion) => [
		{ label: 'Rarity', value: potion.rarity, href: filteredHref(potionsPage, 'rarity', potion.rarity) },
		{ label: 'Drink', value: term(potion.drink).title, html: `${sipsOf(potion)} (+${potion.sips * term(potion.drink).tipsy} ${keyword('tipsy')})` },
		{ label: 'Target', value: potion.target, href: filteredHref(potionsPage, 'target', potion.target) },
		{ label: 'Use', value: potion.usage },
		{ label: 'Character', value: potion.poolName ?? '' },
	],
	group: (potion) => ({ title: potion.rarity, html: filtered(potionsPage, 'rarity', potion.rarity) }),
	dimensions: [
		rarityDimension<Potion>(),
		drinkDimension<Potion>(),
		{ key: 'target', label: 'Target', facet: (potion) => potion.target },
		nameDimension<Potion>(),
	],
};

// --- Keywords -------------------------------------------------------------------------------------------------------

const keywordsPage = 'wiki/keywords/';
/** What each kind of keyword is called. */
const termKinds: Record<string, string> = { drink: 'Drink', stat: 'Stat', power: 'Power', keyword: 'Card keyword', static: 'Game term', orb: 'Orb' };
const termKind = (entry: Term) => termKinds[entry.kind] ?? entry.kind;
const ladderLink = () => link('play/how-it-works/#the-drinks', 'drink ladder');

/** The mod's own keywords, which have pages here. The game's (content.glossary where !local) link to the community wiki. */
export const keywords: Kind<Term> = {
	key: 'keywords',
	ref: 'keyword',
	singular: 'keyword',
	plural: 'keywords',
	page: keywordsPage,
	blurb: 'The drinks, Tipsy and the other words in gold on Tavern content.',
	layout: 'tiles',
	items: content.glossary.filter((entry) => entry.local),
	summary: (entry) => `${entry.title}: a ${lower(termKind(entry))} in Slay the Spirits. ${entry.searchText}`,
	tagline: (entry) => filtered(keywordsPage, 'type', entry.kind, termKind(entry)),
	lead: (entry) =>
		entry.kind === 'drink'
			? `${self('keyword', entry)} is one of the drinks on the ${ladderLink()}.${(entry.tipsy ?? 0) > 0 ? ` Each one adds ${entry.tipsy} ${keyword('tipsy')}.` : ` It doesn't add any ${keyword('tipsy')}.`}`
			: `${self('keyword', entry)} is a ${filtered(keywordsPage, 'type', entry.kind, lower(termKind(entry)))} in Slay the Spirits.`,
	obtain: () => null,
	text: (entry) => entry.body,
	facts: (entry) => [
		{ label: 'Type', value: termKind(entry), href: filteredHref(keywordsPage, 'type', entry.kind) },
		...(entry.kind === 'drink' ? [{ label: 'Tipsy', value: `+${entry.tipsy}`, html: `+${entry.tipsy} ${keyword('tipsy')}` }] : []),
	],
	group: (entry) => ({ title: `${termKind(entry)}s`, html: filtered(keywordsPage, 'type', entry.kind, `${termKind(entry)}s`) }),
	dimensions: [{ key: 'type', label: 'Type', facet: (entry) => entry.kind, display: (kind) => termKinds[kind] ?? kind }, nameDimension<Term>()],
};

// --- All kinds ------------------------------------------------------------------------------------------------------

export const kinds = { cards, relics, potions, keywords } as const;

/** The wiki's front page. */
export const wikiPage = 'wiki/';

/** An entry's page. */
export const entryHref = (kind: KindKey, slug: string) => href(`wiki/${kind}/${slug}/`);

/** The entries before and after one, in its kind's order. */
export function neighbors<T extends Entry>(kind: Kind<T>, item: T): { prev?: T; next?: T } {
	const index = kind.items.findIndex((other) => other.id === item.id);
	return { prev: kind.items[index - 1], next: kind.items[index + 1] };
}

/** Everything that has a keyword among its tooltips, by kind (kinds with nothing are left out). */
export function usedBy(slug: string): { kind: Kind<Entry>; items: Entry[] }[] {
	return (Object.values(kinds) as unknown as Kind<Entry>[])
		.map((kind) => ({ kind, items: kind.items.filter((item) => item.tips.includes(slug)) }))
		.filter((group) => group.items.length > 0);
}

/** A kind's entries under their navigation headings, in the kind's order. */
export function grouped<T extends Entry>(kind: Kind<T>): { group: Group; items: T[] }[] {
	const groups = new Map<string, { group: Group; items: T[] }>();
	for (const item of kind.items) {
		const group = kind.group(item);
		if (!groups.has(group.title)) groups.set(group.title, { group, items: [] });
		groups.get(group.title)!.items.push(item);
	}
	return [...groups.values()];
}
