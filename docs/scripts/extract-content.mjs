// Builds the wiki's data from the mod's own source, so the site can't drift from the game.
//
// Reads:  mod/src/SlayTheSpirits/SlayTheSpiritsMod.cs        (what's registered, and in which pool)
//         mod/src/SlayTheSpirits/{Cards,Relics,Potions,Powers,Events}/*.cs (cost, rarity, keywords, numbers, upgrades,
//                                                              tooltips, event options)
//         mod/src/SlayTheSpirits/Assets/localization/eng/*.json (names and text)
//         mod/src/SlayTheSpirits/Assets/images/**              (icons, copied into public/)
//         src/generated/rules.json                            (the drink ladder, from mod/tools/SlayTheSpirits.DocsExport)
//         src/lib/taxonomy.json                               (how the wiki names and colors pools, rarities, targets)
//         game-assets/                                        (card pieces, keywords and characters from the game,
//                                                              see extract-game-art.mjs)
// Writes: src/generated/content.json (cards, relics, potions, events, glossary, characters),
//         public/generated/images/**, public/generated/cards/** (tinted card layers and card icons)
//
// The C# is read with patterns, not a compiler. Anything it doesn't recognise fails the build with a message saying
// which model and what to fix, rather than publishing a wrong number.

import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { bakeIcons, bakeLayers, iconPath, layerPath } from './card-art.mjs';
import {
	classes,
	constants,
	declaredTips,
	docs,
	entryOf,
	enumProperty,
	flag,
	gameTermId,
	inherited,
	json,
	listProperty,
	loc,
	mod,
	read,
	normalizeTerm,
	registered,
	slugOf,
	taxonomy as readTaxonomy,
} from './lib/mod.mjs';

const errors = [];
const fail = (model, message) => {
	errors.push(`${model}: ${message}`);
	return undefined;
};

const taxonomy = readTaxonomy();
const rules = json(join(docs, 'src/generated/rules.json'));
const gameAssets = join(docs, 'game-assets');
const rerun = 'run `npm run extract-game-art` (needs the game)';
if (!existsSync(join(gameAssets, 'glossary.json'))) {
	console.error(`extract-content: game-assets/glossary.json is missing; ${rerun}`);
	process.exit(1);
}
const game = json(join(gameAssets, 'glossary.json'));

// --- Images -------------------------------------------------------------------------------------------------------

const publicDir = join(docs, 'public');
const publicImages = join(publicDir, 'generated/images');
rmSync(join(publicDir, 'generated'), { recursive: true, force: true });
for (const folder of ['relics', 'potions', 'rest_site', 'powers', 'cards', 'events']) {
	const source = join(mod, 'Assets/images', folder);
	if (existsSync(source)) cpSync(source, join(publicImages, folder), { recursive: true });
}
for (const folder of ['icons', 'characters']) {
	if (existsSync(join(gameAssets, folder))) cpSync(join(gameAssets, folder), join(publicImages, 'game', folder), { recursive: true });
}
/** One of the mod's images, by its model's entry, if it has one. */
const image = (folder, entry) => {
	const file = `${entry.toLowerCase()}.png`;
	return existsSync(join(publicImages, folder, file)) ? `generated/images/${folder}/${file}` : null;
};
/** One of the game's images (a path from game-assets/glossary.json). */
const gameImage = (path) => (path ? `generated/images/game/${path}` : null);

// --- Glossary: every keyword with a tooltip ----------------------------------------------------------------------
//
// The mod's own (the drinks, Tipsy and its powers) get a page on this wiki. The game's (Strength, Block, Exhaust...)
// come from game-assets/glossary.json and link to the community wiki. Text is filled in further down, once every
// keyword is known, because one keyword's text can mention another.

const powerClasses = classes('Powers');
const powerText = loc('powers');
const drinkIcons = new Map(
	[...powerClasses]
		.map(([name, found]) => [found.header.match(/SpiritsTabPower\(PenaltyKind\.(\w+)\)/)?.[1], image('powers', entryOf(name))])
		.filter(([kind]) => kind)
);

const glossary = [];
const termsById = new Map();
const termsByPhrase = new Map();
const termsBySlug = new Map();
function addTerm(term, phrases = []) {
	const slug = slugOf(term.title);
	if (glossary.some((other) => other.slug === slug)) return null; // earlier keywords win (the mod's come first)
	const entry = { ...term, slug, tips: term.tips ?? [], body: '', searchText: '' };
	glossary.push(entry);
	termsById.set(entry.id, entry);
	termsBySlug.set(entry.slug, entry);
	for (const phrase of [term.title, ...phrases]) if (!termsByPhrase.has(normalizeTerm(phrase))) termsByPhrase.set(normalizeTerm(phrase), entry);
	return entry;
}

const tipsyTerm = { id: 'stat:Tipsy', kind: 'stat', title: rules.tipsyTitle, raw: rules.tipsyDescription, local: true, tipsy: null };
tipsyTerm.icon = image('powers', entryOf('SpiritsTipsyPower'));
for (const step of rules.ladder) {
	addTerm(
		{
			id: `drink:${step.kind}`,
			kind: 'drink',
			title: step.singular,
			raw: step.description,
			local: true,
			tipsy: step.tipsy,
			icon: drinkIcons.get(step.kind) ?? fail(`PenaltyKind.${step.kind}`, 'no SpiritsTabPower with an icon in Powers/'),
			tips: step.tipsy > 0 ? [slugOf(tipsyTerm.title)] : [],
		},
		[step.plural, step.kind]
	);
}
addTerm(tipsyTerm);
// The mod's other powers (not the bar tab's, which are the drinks, or Tipsy's, which is the keyword above).
for (const [name, found] of powerClasses) {
	const entry = entryOf(name);
	if (/abstract class|SpiritsTabPower\(/.test(found.header) || !powerText[`${entry}.title`]) continue;
	addTerm({
		id: gameTermId('power', name),
		kind: 'power',
		title: powerText[`${entry}.title`],
		raw: powerText[`${entry}.description`] ?? '',
		markup: true,
		local: true,
		tipsy: null,
		icon: image('powers', entry),
		source: { path: found.path, line: found.line },
	});
}
for (const term of game.terms) {
	addTerm({ id: term.id, kind: term.kind, title: term.title, raw: term.description, markup: true, local: false, wiki: term.wiki, tipsy: null, icon: gameImage(term.icon) });
}

// --- Text ---------------------------------------------------------------------------------------------------------

const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/**
 * Fills {Vars} and turns the game's markup into HTML. `mode` picks which numbers: "base", "upgraded" (changed numbers
 * marked sts-up, as the game shows them green) or "combined" ("2 (3)"). A gold phrase that names a keyword gets
 * data-term="<its slug>", which the site turns into a link or a hover box (src/lib/refs.mjs).
 */
function render(model, text, values = {}, upgraded = {}, mode = 'base', self = null) {
	if (text === undefined) return fail(model, 'missing localization text') ?? '';
	let html = escape(text).replace(/\{(\w+)(?::(\w+)\(\))?\}/g, (whole, name, format) => {
		if (!(name in values)) return fail(model, `text uses {${name}} but the model has no such var`) ?? whole;
		const base = values[name];
		const by = upgraded[name] ?? 0;
		const shown =
			by === 0 || mode === 'base'
				? `${base}`
				: mode === 'upgraded'
					? `<span class="sts-up">${base + by}</span>`
					: `${base} (${base + by})`;
		return format === 'energyIcons' ? `[blue]${shown}[/blue] Energy` : shown;
	});
	html = html
		.replace(/\[gold\]([^[\]<>]+)\[\/gold\]/g, (_, phrase) => {
			const term = termsByPhrase.get(normalizeTerm(phrase));
			return term && term.slug !== self ? `<span class="sts-gold" data-term="${term.slug}">${phrase}</span>` : `<span class="sts-gold">${phrase}</span>`;
		})
		// Colors, and the event text's effects: [jitter] shakes, [sine] sways (src/styles/custom.css).
		.replace(/\[(gold|blue|green|red|purple|orange|aqua|jitter|sine)\]/g, '<span class="sts-$1">')
		.replace(/\[\/(gold|blue|green|red|purple|orange|aqua|jitter|sine)\]/g, '</span>')
		.replace(/\[(\/?)b\]/g, '<$1strong>')
		.replace(/\[(\/?)i\]/g, '<$1em>')
		.replace(/\n/g, '<br>');
	if (/[{}[\]]/.test(html.replace(/<[^>]+>/g, ''))) fail(model, `text has markup the docs don't understand: ${text}`);
	return html;
}

const plain = (html) => html.replace(/<br>/g, ' ').replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/\s+/g, ' ').trim();

for (const term of glossary) {
	term.body = term.markup ? render(term.title, term.raw, {}, {}, 'base', term.slug) : escape(term.raw).replace(/\n/g, '<br>');
	term.searchText = plain(term.body);
	delete term.raw;
	delete term.markup;
}

/** The card's text as the game prints it, keywords included (CardModel.GetDescriptionForPile + CardKeywordOrder). */
function cardFace(description, keywords) {
	const line = (keyword) => {
		const term = termsById.get(gameTermId('keyword', keyword));
		return `<span class="sts-gold"${term ? ` data-term="${term.slug}"` : ''}>${keyword}</span>.`;
	};
	const lines = [description];
	for (const keyword of taxonomy.cardKeywords.before) if (keywords.includes(keyword)) lines.unshift(line(keyword));
	for (const keyword of taxonomy.cardKeywords.after) if (keywords.includes(keyword)) lines.push(line(keyword));
	return lines.filter(Boolean).join('<br>');
}

// --- Numbers ------------------------------------------------------------------------------------------------------

/**
 * CanonicalVars: [new CardsVar(2), new PowerVar<StrengthPower>(2m), new DynamicVar(MaxHeal, 10m)] -> { Cards: 2, ... }.
 * A CalculatedDamageVar (base + extra x something in combat) is its CalculationBaseVar outside combat, so the base
 * is read as CalculatedDamage.
 */
function vars(model, body) {
	const block = body.match(/CanonicalVars\s*=>\s*\[([\s\S]*?)\];/);
	if (!block) return {};
	const consts = constants(body);
	const result = {};
	for (const [, type, generic, args] of block[1].matchAll(/new (\w+?)Var(?:<(\w+)>)?\(([^)]*)\)/g)) {
		const parts = args.split(',').map((s) => s.trim());
		let name;
		let raw;
		if (type === 'Dynamic' || (parts.length > 1 && /^[A-Za-z_"]/.test(parts[0]))) {
			// DynamicVar(Name, 10m), or a typed var given a name: DamageVar(Name, 10m, ValueProp.Unblockable).
			name = consts[parts[0]] ?? parts[0].replace(/"/g, '');
			raw = parts[1];
		} else if (type === 'String' || type === 'CalculatedDamage') {
			continue;
		} else if (type === 'CalculationBase') {
			name = 'CalculatedDamage';
			raw = parts[0];
		} else {
			name = generic ?? type; // PowerVar<StrengthPower> -> StrengthPower, CardsVar -> Cards
			raw = parts[0];
		}
		const value = Number(String(raw).replace(/m$/, ''));
		if (Number.isNaN(value)) fail(model, `can't read the value of ${type}Var(${args})`);
		result[name] = value;
	}
	return result;
}

// DynamicVars.Strength is the StrengthPower var; the rest are named after their type. CalculationBase: see vars().
const varAlias = { Strength: 'StrengthPower', Dexterity: 'DexterityPower', CalculationBase: 'CalculatedDamage' };

/** OnUpgrade: var increases and cost changes, or null if the model has no upgrade. */
function upgrade(model, body) {
	const match = body.match(/OnUpgrade\(\)\s*(?:=>\s*([^;]+;)|\{([\s\S]*?)\n    \})/);
	if (!match) return null;
	const code = match[1] ?? match[2];
	const consts = constants(body);
	const result = { vars: {}, cost: 0 };
	let understood = 0;
	for (const [, named, indexed, by] of code.matchAll(/DynamicVars(?:\.(\w+)|\[([^\]]+)\])\.UpgradeValueBy\((-?[\d.]+)m?\)/g)) {
		const key = named ? (varAlias[named] ?? named) : (consts[indexed] ?? indexed.replace(/"/g, ''));
		result.vars[key] = Number(by);
		understood++;
	}
	for (const [, by] of code.matchAll(/EnergyCost\.UpgradeBy\((-?\d+)\)/g)) {
		result.cost += Number(by);
		understood++;
	}
	const statements = code.split(';').filter((s) => s.trim()).length;
	if (understood !== statements) fail(model, `OnUpgrade has code the docs can't read: ${code.trim()}`);
	return result;
}

/** "StrengthPower" -> "Strength", "MaxHeal" -> "Max Heal": a var's name for people. */
const varLabel = (name) => (name === 'CalculatedDamage' ? 'Damage' : name.replace(/Power$/, '').replace(/([a-z])([A-Z])/g, '$1 $2'));

// --- Tooltips -----------------------------------------------------------------------------------------------------

const ladder = new Map(rules.ladder.map((step) => [step.kind, step]));

/**
 * A model's tooltips, in the game's order, as keyword slugs:
 *   1. its drinks, then Tipsy if one of them adds Tipsy or, with no drinks, if it asks for Tipsy
 *      (Presentation/SpiritsTips.ForPenalties and the models' ExtraHoverTips)
 *   2. the ones it adds itself (AdditionalHoverTips)
 *   3. for cards, their keywords, with Exhaust after Ethereal (CardModel.HoverTips)
 */
function tips(model, map, found, { drinks, wantsTipsy = false, keywords = [] }) {
	const ids = [];
	const distinct = [...new Set(drinks)];
	for (const kind of distinct) {
		if (!ladder.has(kind)) fail(model, `PenaltyKind.${kind} isn't on the drink ladder`);
		ids.push(`drink:${kind}`);
	}
	if (distinct.some((kind) => (ladder.get(kind)?.tipsy ?? 0) > 0) || (wantsTipsy && ids.length === 0)) ids.push(tipsyTerm.id);

	const declared = declaredTips(map, found);
	for (const call of declared.unread) fail(model, `AdditionalHoverTips has a tooltip the docs can't read: ${call}`);
	ids.push(...declared.ids);

	for (const keyword of keywords) {
		ids.push(gameTermId('keyword', keyword));
		if (keyword === 'Ethereal') ids.push(gameTermId('keyword', 'Exhaust'));
	}

	return [...new Set(ids)].flatMap((id) => {
		const term = termsById.get(id);
		if (!term) fail(model, `its tooltip ${id} isn't in game-assets/glossary.json; ${rerun}`);
		return term ? [term.slug] : [];
	});
}

// --- Shared -------------------------------------------------------------------------------------------------------

/** A pool from taxonomy.json, or a build failure naming the pool to add. */
function pool(model, table, className) {
	const found = taxonomy[table][className];
	if (!found) return fail(model, `unknown pool ${className}; add it to ${table} in src/lib/taxonomy.json`);
	return found;
}

function known(model, value, list, what) {
	if (value && !list.includes(value)) fail(model, `unknown ${what} ${value}; add it to src/lib/taxonomy.json`);
	return value;
}

/** Fields every wiki entry has. */
function common(name, found, text, entry) {
	const title = text[`${entry}.title`] ?? fail(name, 'missing title');
	return { id: entry, slug: slugOf(title ?? entry), title, source: { path: found.path, line: found.line } };
}

// --- Characters ---------------------------------------------------------------------------------------------------

const characters = Object.values(taxonomy.cardPools)
	.filter((info) => info.character)
	.map((info) => {
		const found = game.characters.find((character) => character.key === info.key);
		if (!found) return fail(info.name, `the character isn't in game-assets/glossary.json; ${rerun}`);
		return { key: info.key, name: found.shortTitle, title: found.title, color: info.color, icon: gameImage(found.icon), wiki: found.wiki };
	});

// --- Cards --------------------------------------------------------------------------------------------------------

// Every card layer the site needs, baked at the end (scripts/card-art.mjs).
const layers = [];
const looks = [];
const layer = (piece, material) => {
	layers.push({ piece, material });
	return layerPath({ piece, material });
};

/** The pieces the game draws a card from (CardModel.FramePath, PortraitBorderPath, BannerMaterial; NCard). */
function look(model, poolInfo, type, rarity, cost, art) {
	const frameType = taxonomy.cardFrameType[type] ?? fail(model, `no cardFrameType for ${type} in src/lib/taxonomy.json`);
	const style = taxonomy.rarityStyles[rarity] ?? fail(model, `no rarityStyles entry for ${rarity} in src/lib/taxonomy.json`);
	if (!frameType || !style) return null;
	const pieces = {
		frame: layer(`frame_${frameType}`, poolInfo.frame),
		border: layer(`border_${frameType}`, style.banner),
		banner: layer('banner', style.banner),
	};
	looks.push(pieces);
	return {
		...pieces,
		plaque: layer('plaque', style.banner),
		energy: cost < 0 ? null : layer(`energy_${poolInfo.energy}`),
		portrait: art ?? layer('portrait_missing'),
		icon: iconPath(pieces),
		titleOutline: style.titleOutline,
		energyOutline: poolInfo.energyOutline,
	};
}

const cardText = loc('cards');
const cardClasses = classes('Cards');

const cards = registered('Card')
	.map(({ pool: poolClass, name }) => ({ name, poolInfo: pool(name, 'cardPools', poolClass) }))
	.filter(({ poolInfo }) => poolInfo && !poolInfo.hidden)
	.map(({ name, poolInfo }) => {
		const found = cardClasses.get(name);
		if (!found) return fail(name, 'registered but no class found in Cards/');
		// Its own SpiritsCard(...) call, or its base class's (the Tavern curses share SpiritsCurse's).
		const ctor = inherited(cardClasses, found, /SpiritsCard\((-?\d+), CardType\.(\w+), CardRarity\.(\w+), TargetType\.(\w+)\)/);
		if (!ctor) return fail(name, `can't read the SpiritsCard(...) constructor: ${found.header}`);
		const entry = entryOf(name);
		const values = vars(name, found.body);
		const up = upgrade(name, found.body);
		const upVars = up?.vars ?? {};
		const cost = Number(ctor[1]);
		// X cards (HasEnergyCostX) are declared with cost 0 and spend all your energy.
		const costsX = flag(cardClasses, found, 'HasEnergyCostX');
		const keywords = listProperty(cardClasses, found, 'CanonicalKeywords', 'CardKeyword');
		const raw = cardText[`${entry}.description`];
		const text = {
			base: render(name, raw, values, upVars, 'base'),
			upgraded: up ? render(name, raw, values, upVars, 'upgraded') : null,
			combined: render(name, raw, values, upVars, 'combined'),
		};
		return {
			...common(name, found, cardText, entry),
			pool: poolInfo.key,
			poolName: poolInfo.name,
			poolColor: poolInfo.color,
			character: poolInfo.character ? poolInfo.key : null,
			found: poolInfo.character
				? taxonomy.characterFound.replaceAll('{name}', poolInfo.name)
				: /\bISpiritsMajorCurse\b/.test(found.header)
					? taxonomy.majorCurseFound
					: poolInfo.found,
			type: known(name, ctor[2], taxonomy.cardTypes, 'card type'),
			rarity: known(name, ctor[3], taxonomy.rarities, 'rarity'),
			target: taxonomy.targets[ctor[4]] ?? fail(name, `unknown target ${ctor[4]}; add it to src/lib/taxonomy.json`),
			cost: cost < 0 ? null : costsX ? 'X' : cost,
			upgradedCost: !costsX && cost >= 0 && up && up.cost !== 0 ? cost + up.cost : null,
			upgradable: up !== null,
			// What upgrading changes, for the infobox: "Damage 8 → 12".
			upgrade: [
				...Object.entries(upVars).filter(([, by]) => by !== 0).map(([key, by]) => ({ label: varLabel(key), from: values[key], to: values[key] + by })),
				...(cost >= 0 && up && up.cost !== 0 ? [{ label: 'Cost', from: cost, to: cost + up.cost }] : []),
			],
			keywords,
			text,
			face: { base: cardFace(text.base, keywords), upgraded: text.upgraded === null ? null : cardFace(text.upgraded, keywords) },
			searchText: plain(text.combined),
			tips: tips(name, cardClasses, found, {
				drinks: listProperty(cardClasses, found, 'PenaltyKeywords', 'PenaltyKind'),
				wantsTipsy: flag(cardClasses, found, 'ScalesWithTipsy'),
				keywords,
			}),
			image: image('cards', entry),
			look: look(name, poolInfo, ctor[2], ctor[3], cost, image('cards', entry)),
		};
	});

// --- Relics -------------------------------------------------------------------------------------------------------

const relicText = loc('relics');
const relicClasses = classes('Relics');
const relics = registered('Relic').map(({ pool: poolClass, name }) => {
	const found = relicClasses.get(name);
	if (!found) return fail(name, 'registered but no class found in Relics/');
	const poolInfo = pool(name, 'relicPools', poolClass) ?? {};
	const entry = entryOf(name);
	const description = render(name, relicText[`${entry}.description`], vars(name, found.body));
	return {
		...common(name, found, relicText, entry),
		pool: poolInfo.key,
		poolName: poolInfo.name,
		found: poolInfo.found,
		rarity: known(name, enumProperty(relicClasses, found, 'Rarity', 'RelicRarity') ?? fail(name, "can't read its rarity"), taxonomy.rarities, 'rarity'),
		description,
		searchText: plain(description),
		flavor: relicText[`${entry}.flavor`] ?? '',
		tips: tips(name, relicClasses, found, {
			drinks: listProperty(relicClasses, found, 'PenaltyKeywords', 'PenaltyKind'),
			wantsTipsy: flag(relicClasses, found, 'MentionsTipsy'),
		}),
		image: image('relics', entry),
	};
});

// --- Potions ------------------------------------------------------------------------------------------------------

const potionText = loc('potions');
const potionClasses = classes('Potions');
const potions = registered('Potion').map(({ pool: poolClass, name }) => {
	const found = potionClasses.get(name);
	if (!found) return fail(name, 'registered but no class found in Potions/');
	const poolInfo = pool(name, 'potionPools', poolClass) ?? {};
	// Potions only cost Sips: `SpiritsPotion(2)` is two of them.
	const sips = Number(found.header.match(/SpiritsPotion\((\d+)\)/)?.[1]);
	const drink = sips > 0 ? 'Sip' : undefined;
	const rarity = enumProperty(potionClasses, found, 'Rarity', 'PotionRarity');
	const target = enumProperty(potionClasses, found, 'TargetType', 'TargetType');
	const usage = enumProperty(potionClasses, found, 'Usage', 'PotionUsage');
	if (!drink || !rarity || !target || !usage) return fail(name, "can't read its drink, rarity, target or usage");
	const entry = entryOf(name);
	const description = render(name, potionText[`${entry}.description`], vars(name, found.body));
	return {
		...common(name, found, potionText, entry),
		pool: poolInfo.key,
		poolName: poolInfo.name,
		found: poolInfo.found,
		drink: termsById.get(`drink:${drink}`)?.slug ?? fail(name, `PenaltyKind.${drink} isn't on the drink ladder`),
		sips,
		rarity: known(name, rarity, taxonomy.rarities, 'rarity'),
		target: taxonomy.targets[target] ?? fail(name, `unknown target ${target}; add it to src/lib/taxonomy.json`),
		usage: taxonomy.potionUsage[usage] ?? fail(name, `unknown usage ${usage}; add it to src/lib/taxonomy.json`),
		description,
		searchText: plain(description),
		tips: tips(name, potionClasses, found, { drinks: [drink] }),
		image: image('potions', entry),
	};
});

// --- Events -------------------------------------------------------------------------------------------------------
//
// From each event's GenerateInitialOptions: its options in order (new EventOption(this, Method, InitialOptionKey("X"))),
// where a null method with an X_LOCKED key is the locked version of the option before it, and from each option's
// method, the pages it can end on (FinishOn("PAGE")). Text comes from events.json; vars the event sets in
// CalculateVars (from the player's stats) are described by eventVars in src/lib/taxonomy.json.

const eventText = loc('events');
const eventClasses = classes('Events');
const modFile = read(join(mod, 'SlayTheSpiritsMod.cs'));
/** "BLUFF_IN" after "BLUFF" -> "In": what tells an option's outcomes apart. */
const outcomeLabel = (option, page) =>
	page === option
		? null
		: page
				.replace(new RegExp(`^${option}_`), '')
				.toLowerCase()
				.replace(/(^|_)(\w)/g, (_, gap, letter) => `${gap ? ' ' : ''}${letter.toUpperCase()}`);
/** The keyword slugs an event's text marks, in order, with Tipsy after them if a drink adds it. */
const termsIn = (html) => [...new Set([...html.matchAll(/data-term="([^"]+)"/g)].map((m) => m[1]))];

const events = [...modFile.matchAll(/\.AddEvent<(\w+)>\(\)/g)].map(([, name]) => {
	const found = eventClasses.get(name);
	if (!found) return fail(name, 'registered but no class found in Events/');
	if (/\bIsAllowed\b/.test(found.body)) fail(name, 'it has its own IsAllowed; say where it appears in the docs (eventFound in src/lib/taxonomy.json)');
	const entry = entryOf(name);
	const key = (rest) => `${entry}.pages.${rest}`;
	const values = vars(name, found.body);
	const calculated = found.body.match(/CalculateVars\(\)[\s\S]*?;\s*\n/)?.[0] ?? '';
	for (const [, constant] of calculated.matchAll(/DynamicVars\[(\w+)\]\.BaseValue\s*=/g)) {
		const varName = constants(found.body)[constant] ?? constant;
		values[varName] = taxonomy.eventVars[`${entry}.${varName}`] ?? fail(name, `${varName} is set in CalculateVars; describe it in eventVars in src/lib/taxonomy.json`);
	}
	const text = (rest) => render(name, eventText[key(rest)], values);

	const generate = found.body.match(/GenerateInitialOptions\(\)\s*=>\s*\[([\s\S]*?)\n    \];/)?.[1];
	if (!generate) return fail(name, "can't read GenerateInitialOptions");
	const options = [];
	for (const [, method, option] of generate.matchAll(/new EventOption\(this, (\w+), InitialOptionKey\("(\w+)"\)\)/g)) {
		if (method === 'null') {
			const unlocked = options.find((other) => `${other.key}_LOCKED` === option);
			if (!unlocked) fail(name, `locked option ${option} doesn't follow the option it locks`);
			else unlocked.locked = { title: eventText[key(`INITIAL.options.${option}.title`)], text: text(`INITIAL.options.${option}.description`) };
			continue;
		}
		const body = found.body.match(new RegExp(`Task ${method}\\(\\)\\s*\\{([\\s\\S]*?)\\n    \\}`))?.[1] ?? fail(name, `can't find the method ${method}`) ?? '';
		const pages = [...body.matchAll(/FinishOn\("(\w+)"\)/g)].map((m) => m[1]);
		if (pages.length === 0) fail(name, `${method} never calls FinishOn(...), so the docs can't show what happens`);
		options.push({
			key: option,
			title: eventText[key(`INITIAL.options.${option}.title`)] ?? fail(name, `missing the title of option ${option}`),
			text: text(`INITIAL.options.${option}.description`),
			locked: null,
			outcomes: pages.map((page) => ({ label: outcomeLabel(option, page), text: text(`${page}.description`) })),
		});
	}
	if (options.length === 0) fail(name, "can't read any options in GenerateInitialOptions");

	const intro = text('INITIAL.description');
	const all = [intro, ...options.flatMap((option) => [option.text, ...option.outcomes.map((outcome) => outcome.text)])].join(' ');
	const drinks = termsIn(options.map((option) => option.text).join(' '));
	const addsTipsy = drinks.some((slug) => (termsBySlug.get(slug)?.tipsy ?? 0) > 0);
	return {
		...common(name, found, eventText, entry),
		found: taxonomy.eventFound,
		acts: taxonomy.eventActs,
		intro,
		options,
		searchText: plain(all),
		tips: [...drinks, ...(addsTipsy ? [slugOf(tipsyTerm.title)] : [])].filter((slug, i, list) => list.indexOf(slug) === i),
		image: image('events', entry),
	};
});

// --- Checks -------------------------------------------------------------------------------------------------------

const kinds = { cards, relics, potions, events, keywords: glossary.filter((term) => term.local) };

for (const [kind, list] of Object.entries(kinds)) {
	const seen = new Map();
	for (const item of list.filter(Boolean)) {
		if (seen.has(item.slug)) fail(item.id, `its page /wiki/${kind}/${item.slug}/ clashes with ${seen.get(item.slug)}`);
		seen.set(item.slug, item.id);
	}
}

// Hand-written notes (src/content/notes/<kind>/<slug>.md) are shown on the matching wiki page. One whose entry was
// renamed or removed would silently vanish, so it fails the build instead.
const notesRoot = join(docs, 'src/content/notes');
for (const [kind, list] of Object.entries(kinds)) {
	const folder = join(notesRoot, kind);
	if (!existsSync(folder)) continue;
	const slugs = new Set(list.filter(Boolean).map((item) => item.slug));
	for (const file of readdirSync(folder).filter((f) => /\.mdx?$/.test(f))) {
		const slug = file.replace(/\.mdx?$/, '');
		if (!slugs.has(slug)) fail(`notes/${kind}/${file}`, `nothing has the page /wiki/${kind}/${slug}/`);
	}
}

// --- Output -------------------------------------------------------------------------------------------------------

if (errors.length === 0) {
	errors.push(...(await bakeLayers(layers, { pieces: join(gameAssets, 'cards'), publicDir })));
	if (errors.length === 0) await bakeIcons(looks, { publicDir, portraitColor: taxonomy.cardColors.cream });
}

if (errors.length > 0) {
	console.error(`extract-content: ${errors.length} problem(s) reading the mod:\n  ${errors.join('\n  ')}`);
	process.exit(1);
}

const out = join(docs, 'src/generated/content.json');
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ cards, relics, potions, events, glossary, characters }, null, '\t'));
console.log(
	`extract-content: ${cards.length} cards, ${relics.length} relics, ${potions.length} potions, ${events.length} events, ${glossary.length} keywords -> ${out}`
);
