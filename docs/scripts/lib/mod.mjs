// Reads the mod's C# source and localization with patterns (not a compiler). Shared by extract-content.mjs (every
// build) and extract-game-art.mjs (run by hand, to find which of the game's keywords the mod's content refers to).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const docs = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const repoRoot = resolve(docs, '..');
export const mod = resolve(repoRoot, 'mod/src/SlayTheSpirits');

export const read = (path) => readFileSync(path, 'utf8');
export const json = (path) => JSON.parse(read(path));
/** One of the mod's English localization tables. */
export const loc = (table) => json(join(mod, 'Assets/localization/eng', `${table}.json`));
export const taxonomy = () => json(join(docs, 'src/lib/taxonomy.json'));

/** SpiritsHipFlask -> SPIRITS_HIP_FLASK (the game's id rule: class name, words split on capitals). */
export const entryOf = (className) => className.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();

/** "Cheers!" -> "cheers", "Hair of the Dog" -> "hair-of-the-dog": a page's URL. */
export const slugOf = (title) =>
	title
		.normalize('NFKD')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-|-$/g, '');

/**
 * A phrase reduced to what identifies a keyword, so text can be matched to it however it's worded: lowercase,
 * without possessives, singular. "Sips" -> "sip", "finishes their drink" and "Finish Your Drink" -> "finish drink".
 */
export const normalizeTerm = (phrase) =>
	phrase
		.toLowerCase()
		.replace(/[^a-z0-9 ]+/g, ' ')
		.split(/\s+/)
		.filter((word) => word && !['your', 'their', 'his', 'her', 'my', 'a', 'an', 'the'].includes(word))
		.map((word) => (/(sh|ch|x|ss)es$/.test(word) ? word.slice(0, -2) : /[^s]s$/.test(word) ? word.slice(0, -1) : word))
		.join(' ');

// --- Registration -------------------------------------------------------------------------------------------------

/** What SlayTheSpiritsMod.cs registers: .AddCard<ColorlessCardPool, SpiritsCheers>() -> { pool, name }. */
export function registered(kind) {
	const modFile = read(join(mod, 'SlayTheSpiritsMod.cs'));
	return [...modFile.matchAll(new RegExp(`\\.Add${kind}<(\\w+), (\\w+)>\\(\\)`, 'g'))].map(([, pool, name]) => ({ pool, name }));
}

// --- Class bodies -------------------------------------------------------------------------------------------------

/** Every top-level class in a folder of the mod: name -> { name, header, body, base, path, line }. */
export function classes(folder) {
	const found = new Map();
	for (const file of readdirSync(join(mod, folder)).filter((f) => f.endsWith('.cs'))) {
		const text = read(join(mod, folder, file));
		const starts = [...text.matchAll(/^public (?:sealed |abstract )?class (\w+)[^\n]*$/gm)];
		starts.forEach((match, i) => {
			const end = i + 1 < starts.length ? starts[i + 1].index : text.length;
			found.set(match[1], {
				name: match[1],
				header: match[0],
				body: text.slice(match.index, end),
				base: match[0].match(/\)?\s*:\s*(\w+)/)?.[1] ?? null,
				path: relative(repoRoot, join(mod, folder, file)).replace(/\\/g, '/'),
				line: text.slice(0, match.index).split('\n').length,
			});
		});
	}
	return found;
}

/** The first match of a pattern in a class or the classes it inherits from (within the same folder). */
export function inherited(map, found, pattern) {
	for (let current = found; current; current = map.get(current.base)) {
		const match = current.body.match(pattern);
		if (match) return match;
	}
	return null;
}

export const constants = (body) =>
	Object.fromEntries([...body.matchAll(/const string (\w+) = "(\w+)";/g)].map(([, k, v]) => [k, v]));

const enumList = (block, type) => [...(block ?? '').matchAll(new RegExp(`${type}\\.(\\w+)`, 'g'))].map((m) => m[1]);

/** A property that is a list of enum values: CanonicalKeywords => [CardKeyword.Exhaust] -> ["Exhaust"]. */
export const listProperty = (map, found, property, type) =>
	enumList(inherited(map, found, new RegExp(`${property}\\s*=>\\s*\\[([^\\]]*)\\]`))?.[1], type);

export const flag = (map, found, property) =>
	inherited(map, found, new RegExp(`${property}\\s*=>\\s*(true|false)`))?.[1] === 'true';

export const enumProperty = (map, found, property, type) =>
	inherited(map, found, new RegExp(`${property}\\s*=>\\s*${type}\\.(\\w+)`))?.[1] ?? null;

// --- Tooltips -----------------------------------------------------------------------------------------------------

/**
 * The game tooltips a model adds in AdditionalHoverTips, as ids of the game's keywords (see gameTermId):
 * HoverTipFactory.FromPower<StrengthPower>() -> "power:STRENGTH_POWER", .Static(StaticHoverTip.Block) ->
 * "static:BLOCK", .FromKeyword(CardKeyword.Exhaust) -> "keyword:EXHAUST", .FromOrb<FrostOrb>() -> "orb:FROST_ORB",
 * .ForEnergy(this) -> "static:ENERGY". Returns { ids, unread }: unread is any call it didn't recognise.
 */
export function declaredTips(map, found) {
	const block = inherited(map, found, /AdditionalHoverTips\s*=>\s*\[([\s\S]*?)\];/)?.[1] ?? '';
	const ids = [];
	const calls = [...block.matchAll(/HoverTipFactory\.(\w+)(?:<(\w+)>)?\(([^)]*)\)/g)];
	const unread = [];
	for (const [whole, method, generic, args] of calls) {
		if (method === 'FromPower' && generic) ids.push(gameTermId('power', generic));
		else if (method === 'FromOrb' && generic) ids.push(gameTermId('orb', generic));
		else if (method === 'Static') ids.push(gameTermId('static', args.match(/StaticHoverTip\.(\w+)/)?.[1] ?? ''));
		else if (method === 'FromKeyword') ids.push(gameTermId('keyword', args.match(/CardKeyword\.(\w+)/)?.[1] ?? ''));
		else if (method === 'ForEnergy') ids.push(gameTermId('static', 'Energy'));
		else unread.push(whole);
	}
	if (block.replace(/HoverTipFactory\.\w+(?:<\w+>)?\([^)]*\)/g, '').replace(/[\s,]/g, '')) unread.push(block.trim());
	return { ids, unread };
}

/** The id of one of the game's keywords: its kind and its localization entry (the class or enum name, slugified). */
export const gameTermId = (kind, name) => `${kind}:${entryOf(name)}`;

/** Every [gold]phrase[/gold] in a localization table's values. */
export const goldPhrases = (table) =>
	Object.values(table).flatMap((text) => [...String(text).matchAll(/\[gold\]([^[\]{}]+)\[\/gold\]/g)].map((m) => m[1]));
