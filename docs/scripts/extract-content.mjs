// Builds the wiki's card, relic and potion data from the mod's own source, so the site can't drift from the game.
//
// Reads:  mod/src/SlayTheSpirits/SlayTheSpiritsMod.cs        (what's registered, and in which pool)
//         mod/src/SlayTheSpirits/{Cards,Relics,Potions}/*.cs  (cost, rarity, keywords, numbers, upgrades)
//         mod/src/SlayTheSpirits/Assets/localization/eng/*.json (names and text)
//         mod/src/SlayTheSpirits/Assets/images/**              (icons, copied into public/)
// Writes: src/generated/content.json, public/generated/images/**
//
// The C# is read with patterns, not a compiler. Anything it doesn't recognise fails the build with a message saying
// which model and what to fix, rather than publishing a wrong number.

import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const docs = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const mod = resolve(docs, '../mod/src/SlayTheSpirits');
const errors = [];
const fail = (model, message) => errors.push(`${model}: ${message}`);

const read = (path) => readFileSync(path, 'utf8');
const loc = (table) => JSON.parse(read(join(mod, 'Assets/localization/eng', `${table}.json`)));

// SpiritsHipFlask -> SPIRITS_HIP_FLASK (the game's id rule: class name, words split on capitals).
const entryOf = (className) => className.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();

// --- Registration -------------------------------------------------------------------------------------------------

const modFile = read(join(mod, 'SlayTheSpiritsMod.cs'));
const registered = (kind) =>
	[...modFile.matchAll(new RegExp(`\\.Add${kind}<(\\w+), (\\w+)>\\(\\)`, 'g'))].map(([, pool, name]) => ({ pool, name }));

// --- Class bodies -------------------------------------------------------------------------------------------------

/** Every top-level class in a folder: name -> { header, body, doc }. */
function classes(folder) {
	const found = new Map();
	for (const file of readdirSync(join(mod, folder)).filter((f) => f.endsWith('.cs'))) {
		const text = read(join(mod, folder, file));
		const starts = [...text.matchAll(/^public (?:sealed |abstract )?class (\w+)[^\n]*$/gm)];
		starts.forEach((match, i) => {
			const end = i + 1 < starts.length ? starts[i + 1].index : text.length;
			found.set(match[1], { header: match[0], body: text.slice(match.index, end), file: `${folder}/${file}` });
		});
	}
	return found;
}

const constants = (body) => Object.fromEntries([...body.matchAll(/const string (\w+) = "(\w+)";/g)].map(([, k, v]) => [k, v]));

/** CanonicalVars: [new CardsVar(2), new PowerVar<StrengthPower>(2m), new DynamicVar(MaxHeal, 10m)] -> { Cards: 2, ... }. */
function vars(model, body) {
	const block = body.match(/CanonicalVars\s*=>\s*\[([\s\S]*?)\];/);
	if (!block) return {};
	const consts = constants(body);
	const result = {};
	for (const [, type, generic, args] of block[1].matchAll(/new (\w+?)Var(?:<(\w+)>)?\(([^)]*)\)/g)) {
		const parts = args.split(',').map((s) => s.trim());
		let name;
		let raw;
		if (type === 'Dynamic') {
			name = consts[parts[0]] ?? parts[0].replace(/"/g, '');
			raw = parts[1];
		} else if (type === 'String') {
			continue;
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

// DynamicVars.Strength is the StrengthPower var; the rest are named after their type.
const varAlias = { Strength: 'StrengthPower', Dexterity: 'DexterityPower' };

/** OnUpgrade: var increases and cost changes. */
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

const keywords = (body) =>
	[...(body.match(/CanonicalKeywords\s*=>\s*\[([^\]]*)\]/)?.[1] ?? '').matchAll(/CardKeyword\.(\w+)/g)].map((m) => m[1]);

// --- Text ---------------------------------------------------------------------------------------------------------

const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** Fills {Vars} ("2 (3)" when an upgrade changes it) and turns the game's markup into HTML. */
function render(model, text, values = {}, upgraded = {}) {
	if (text === undefined) {
		fail(model, 'missing localization text');
		return '';
	}
	let html = escape(text).replace(/\{(\w+)(?::(\w+)\(\))?\}/g, (whole, name, format) => {
		if (!(name in values)) {
			fail(model, `text uses {${name}} but the model has no such var`);
			return whole;
		}
		const base = values[name];
		const shown = name in upgraded && upgraded[name] !== 0 ? `${base} (${base + upgraded[name]})` : `${base}`;
		return format === 'energyIcons' ? `[blue]${shown}[/blue] Energy` : shown;
	});
	html = html
		.replace(/\[(gold|blue|green|red|purple|orange)\]/g, '<span class="sts-$1">')
		.replace(/\[\/(gold|blue|green|red|purple|orange)\]/g, '</span>')
		.replace(/\n/g, '<br>');
	if (/[{}[\]]/.test(html.replace(/<[^>]+>/g, ''))) fail(model, `text has markup the docs don't understand: ${text}`);
	return html;
}

// --- Images -------------------------------------------------------------------------------------------------------

const publicImages = join(docs, 'public/generated/images');
rmSync(publicImages, { recursive: true, force: true });
for (const folder of ['relics', 'potions', 'rest_site', 'powers', 'cards']) {
	const source = join(mod, 'Assets/images', folder);
	if (existsSync(source)) cpSync(source, join(publicImages, folder), { recursive: true });
}
const image = (folder, entry) => {
	const file = `${entry.toLowerCase()}.png`;
	return existsSync(join(publicImages, folder, file)) ? `generated/images/${folder}/${file}` : null;
};

// --- Cards --------------------------------------------------------------------------------------------------------

const cardText = loc('cards');
const cardClasses = classes('Cards');
const poolNames = {
	ColorlessCardPool: 'Colorless',
	CurseCardPool: 'Curse',
	StatusCardPool: 'Status',
	IroncladCardPool: 'Ironclad',
	SilentCardPool: 'Silent',
	DefectCardPool: 'Defect',
	RegentCardPool: 'Regent',
	NecrobinderCardPool: 'Necrobinder',
	FoolCardPool: 'Fool',
};

const cards = registered('Card')
	// Drink cards are handed out by the drinking game itself; the ladder page covers them.
	.filter(({ pool }) => pool !== 'StatusCardPool')
	.map(({ pool, name }) => {
		const found = cardClasses.get(name);
		if (!found) return fail(name, 'registered but no class found in Cards/');
		const ctor = found.header.match(/SpiritsCard\((-?\d+), CardType\.(\w+), CardRarity\.(\w+), TargetType\.(\w+)\)/);
		if (!ctor) return fail(name, `can't read the SpiritsCard(...) constructor: ${found.header}`);
		const entry = entryOf(name);
		const values = vars(name, found.body);
		const up = upgrade(name, found.body) ?? { vars: {}, cost: 0 };
		const cost = Number(ctor[1]);
		return {
			id: entry,
			title: cardText[`${entry}.title`] ?? fail(name, 'missing title'),
			description: render(name, cardText[`${entry}.description`], values, up.vars),
			cost: cost < 0 ? null : cost,
			upgradedCost: cost >= 0 && up.cost !== 0 ? cost + up.cost : null,
			type: ctor[2],
			rarity: ctor[3],
			keywords: keywords(found.body),
			pool: poolNames[pool] ?? fail(name, `unknown pool ${pool}; add it to poolNames`),
			character: /ISpiritsCharacterCard/.test(found.header),
			image: image('cards', entry),
		};
	});

// --- Relics -------------------------------------------------------------------------------------------------------

const relicText = loc('relics');
const relicClasses = classes('Relics');
const relics = registered('Relic').map(({ pool, name }) => {
	const found = relicClasses.get(name);
	if (!found) return fail(name, 'registered but no class found in Relics/');
	const entry = entryOf(name);
	return {
		id: entry,
		title: relicText[`${entry}.title`] ?? fail(name, 'missing title'),
		description: render(name, relicText[`${entry}.description`], vars(name, found.body)),
		flavor: relicText[`${entry}.flavor`] ?? '',
		source: pool === 'EventRelicPool' ? 'Rest site Shot' : 'Relic rewards',
		image: image('relics', entry),
	};
});

// --- Potions ------------------------------------------------------------------------------------------------------

const potionText = loc('potions');
const potionClasses = classes('Potions');
const potions = registered('Potion').map(({ name }) => {
	const found = potionClasses.get(name);
	if (!found) return fail(name, 'registered but no class found in Potions/');
	const drink = found.header.match(/SpiritsPotion\(PenaltyKind\.(\w+)\)/)?.[1];
	const rarity = found.body.match(/PotionRarity\.(\w+)/)?.[1];
	if (!drink || !rarity) return fail(name, "can't read its drink or rarity");
	const entry = entryOf(name);
	return {
		id: entry,
		title: potionText[`${entry}.title`] ?? fail(name, 'missing title'),
		description: render(name, potionText[`${entry}.description`], vars(name, found.body)),
		drink,
		rarity,
		image: image('potions', entry),
	};
});

// --- Output -------------------------------------------------------------------------------------------------------

if (errors.length > 0) {
	console.error(`extract-content: ${errors.length} problem(s) reading the mod:\n  ${errors.join('\n  ')}`);
	process.exit(1);
}

const out = join(docs, 'src/generated/content.json');
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ cards, relics, potions }, null, '\t'));
console.log(`extract-content: ${cards.length} cards, ${relics.length} relics, ${potions.length} potions -> ${out}`);
