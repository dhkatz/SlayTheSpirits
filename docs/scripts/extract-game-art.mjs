// Extracts what the docs need from the game into game-assets/, which is committed because the docs build can't read
// the game:
//
//   cards/          the pieces the game draws cards from (frames, banners, portrait borders, type plaque, energy
//                   orbs, the missing-art portrait) and its color materials; the build tints them (card-art.mjs)
//   glossary.json   the game's keywords the mod's content refers to (Strength, Block, Exhaust...): their text, and
//                   whether the community wiki has a page for them; plus the characters
//   icons/, characters/   their icons
//
// Run it after a game update, after adding a pool or rarity to src/lib/taxonomy.json, or when the build says a
// keyword is missing:
//
//   npm run extract-game-art
//
// The game folder is the one the build uses (STS2_GAME_DIR, local.props or Steam; see build/Sts2.GameDir.props).
// Godot comes from the GODOT_EXE environment variable or GodotExe in ../local.props.
// It asks the community wiki which pages exist (--offline skips that and keeps the links it already had).

import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { communityWiki } from '../src/lib/site.mjs';
import {
	classes,
	declaredTips,
	docs,
	gameTermId,
	goldPhrases,
	json,
	listProperty,
	loc,
	normalizeTerm,
	registered,
	repoRoot,
	taxonomy as readTaxonomy,
} from './lib/mod.mjs';

const out = join(docs, 'game-assets');
const taxonomy = readTaxonomy();
const offline = process.argv.includes('--offline');

function setting(env, property) {
	if (process.env[env]) return process.env[env];
	const props = join(repoRoot, 'local.props');
	const value = existsSync(props) && readFileSync(props, 'utf8').match(new RegExp(`<${property}>([^<]+)</${property}>`))?.[1];
	if (!value) throw new Error(`Set ${env}, or ${property} in local.props`);
	return value.trim();
}

// The game folder comes from the build, which also checks STS2_GAME_DIR, local.props and Steam.
function gameDir() {
	const out = execFileSync('dotnet', ['msbuild', join(repoRoot, 'mod/src/SlayTheSpirits'), '-nologo', '-getProperty:Sts2GameDir'], {
		encoding: 'utf8',
	}).trim();
	if (!out) throw new Error('Couldn\'t find Slay the Spire 2. Set STS2_GAME_DIR, or Sts2GameDir in local.props');
	return out;
}

const pck = join(gameDir(), 'SlayTheSpire2.pck');
const godot = setting('GODOT_EXE', 'GodotExe');

// Godot runs a script from a project, so give it an empty one.
const project = mkdtempSync(join(tmpdir(), 'sts-art-'));
writeFileSync(join(project, 'project.godot'), 'config_version=5\n');
let run = 0;
function extract(manifest, target) {
	const file = join(project, `manifest-${run++}.json`);
	writeFileSync(file, JSON.stringify(manifest, null, '\t'));
	execFileSync(godot, ['--headless', '--path', project, '--script', join(docs, 'scripts/game-art/extract.gd'), '--', pck, file, target], {
		stdio: 'inherit',
	});
}

try {
	// --- 1. The game's localization, to look keywords up in (not kept) ------------------------------------------
	const tableNames = ['card_keywords', 'static_hover_tips', 'powers', 'orbs', 'characters'];
	const dump = join(project, 'tables');
	extract({ files: Object.fromEntries(tableNames.map((name) => [`${name}.json`, `res://localization/eng/${name}.json`])) }, dump);
	const table = Object.fromEntries(tableNames.map((name) => [name, json(join(dump, `${name}.json`))]));

	// --- 2. Which keywords the mod's content refers to ---------------------------------------------------------

	// Everything in the game that has a tooltip with plain text: id -> { kind, title, description, icons }.
	// Order matters: when two share a title, the first is the one a word in a description means.
	const candidates = new Map();
	const add = (kind, source, icons) => {
		for (const key of Object.keys(source).filter((k) => k.endsWith('.title'))) {
			const entry = key.slice(0, -'.title'.length);
			const [title, description] = [source[key], source[`${entry}.description`]];
			if (!description || /[{}]/.test(title + description)) continue; // text with variables needs the game to fill in
			candidates.set(`${kind}:${entry}`, { kind, title, description, icons: icons(entry.toLowerCase()) });
		}
	};
	add('keyword', table.card_keywords, () => []);
	add('static', table.static_hover_tips, () => []);
	add('power', table.powers, (entry) => [`res://images/powers/${entry}.png`, `res://images/atlases/power_atlas.sprites/${entry}.tres`]);
	add('orb', table.orbs, (entry) => [`res://images/orbs/${entry}.png`]);

	const wanted = new Set();
	const problems = [];

	// Tooltips the mod's models declare, and the keywords on its cards (the game adds Exhaust's tip to Ethereal cards).
	const ownPowers = classes('Powers');
	for (const [kind, folder] of [['Card', 'Cards'], ['Relic', 'Relics'], ['Potion', 'Potions']]) {
		const map = classes(folder);
		for (const { name } of registered(kind)) {
			const found = map.get(name);
			if (!found) continue;
			const { ids } = declaredTips(map, found);
			for (const id of ids) if (!ownPowers.has(classNameOf(id, ownPowers))) wanted.add(id);
			const keywords = listProperty(map, found, 'CanonicalKeywords', 'CardKeyword');
			for (const keyword of keywords) wanted.add(gameTermId('keyword', keyword));
			if (keywords.includes('Ethereal')) wanted.add(gameTermId('keyword', 'Exhaust'));
		}
	}
	for (const id of wanted) if (!candidates.has(id)) problems.push(`the mod uses ${id}, but the game has no plain tooltip text for it`);

	// Words the mod's text marks gold that are the title of one of the game's keywords.
	const byTitle = new Map();
	for (const [id, candidate] of candidates) if (!byTitle.has(normalizeTerm(candidate.title))) byTitle.set(normalizeTerm(candidate.title), id);
	for (const name of ['cards', 'relics', 'potions', 'powers', 'events']) {
		for (const phrase of goldPhrases(loc(name))) {
			const id = byTitle.get(normalizeTerm(phrase));
			if (id) wanted.add(id);
		}
	}

	if (problems.length) throw new Error(problems.join('\n'));

	// --- 3. Characters -------------------------------------------------------------------------------------------
	const characters = Object.values(taxonomy.cardPools)
		.filter((pool) => pool.character)
		.map((pool) => {
			const entry = pool.key.toUpperCase();
			const title = table.characters[`${entry}.title`];
			if (!title) throw new Error(`no character ${entry} in the game's characters table (pool ${pool.name})`);
			return { key: pool.key, title, shortTitle: table.characters[`${entry}.shortTitle`] ?? pool.name };
		});

	// --- 4. Textures and materials -------------------------------------------------------------------------------
	const sprite = (name) => `res://images/atlases/ui_atlas.sprites/card/${name}.tres`;
	const textures = {
		'cards/banner': { paths: [sprite('card_banner')] },
		'cards/plaque': { paths: ['res://images/ui/cards/card_portrait_border_plaque2.png'] },
		'cards/portrait_missing': { paths: ['res://images/atlases/card_atlas.sprites/beta.tres', 'res://images/packed/card_portraits/beta.png'] },
	};
	for (const type of new Set(Object.values(taxonomy.cardFrameType))) {
		textures[`cards/frame_${type}`] = { paths: [sprite(`card_frame_${type}_s`)] };
		textures[`cards/border_${type}`] = { paths: [sprite(`card_portrait_border_${type}_s`)] };
	}
	for (const pool of Object.values(taxonomy.cardPools)) {
		textures[`cards/energy_${pool.energy}`] = { paths: [sprite(`energy_${pool.energy}`), `res://images/packed/energy_icons/energy_${pool.energy}.png`] };
	}
	const iconName = (id) => `icons/${id.split(':')[1].toLowerCase()}`;
	for (const id of wanted) {
		const { icons } = candidates.get(id);
		if (icons.length) textures[iconName(id)] = { paths: icons, optional: true };
	}
	for (const character of characters) {
		textures[`characters/${character.key}`] = { paths: [`res://images/ui/top_panel/character_icon_${character.key}.png`], optional: true };
	}

	const materials = {};
	for (const pool of Object.values(taxonomy.cardPools)) materials[pool.frame] = `res://materials/cards/frames/${pool.frame}_mat.tres`;
	for (const rarity of Object.values(taxonomy.rarityStyles)) materials[rarity.banner] = `res://materials/cards/banners/${rarity.banner}_mat.tres`;

	const previous = existsSync(join(out, 'glossary.json')) ? json(join(out, 'glossary.json')) : { terms: [], characters: [] };
	rmSync(out, { recursive: true, force: true });
	mkdirSync(out, { recursive: true });
	extract({ textures, materials, materialsOut: 'cards/materials.json' }, out);

	// --- 5. The glossary -----------------------------------------------------------------------------------------
	const iconOf = (name) => (existsSync(join(out, `${name}.png`)) ? `${name}.png` : null);
	const terms = [...wanted].sort().map((id) => {
		const { kind, title, description } = candidates.get(id);
		return { id, kind, title, description, icon: iconOf(iconName(id)), wiki: null };
	});
	const people = characters.map((character) => ({ ...character, icon: iconOf(`characters/${character.key}`), wiki: null }));

	// Link each to its community wiki page, if it has one.
	const pages = new Map([...terms.map((t) => [t, t.title]), ...people.map((c) => [c, c.shortTitle])]);
	const existing = offline ? null : await wikiPages([...new Set(pages.values())]);
	const before = new Map([...previous.terms.map((t) => [t.id, t.wiki]), ...previous.characters.map((c) => [`character:${c.key}`, c.wiki])]);
	for (const [entry, title] of pages) {
		entry.wiki = existing ? (existing.get(title) ?? null) : (before.get(entry.id ?? `character:${entry.key}`) ?? null);
	}

	writeFileSync(join(out, 'glossary.json'), JSON.stringify({ terms, characters: people }, null, '\t') + '\n');
	console.log(`extract-game-art: ${terms.length} keywords, ${people.length} characters -> ${out}`);
	for (const entry of [...terms, ...people]) if (!entry.wiki) console.log(`  no community wiki page for ${entry.title}`);
} finally {
	rmSync(project, { recursive: true, force: true });
}

/** "power:STRENGTH_POWER" -> the name of a class in `map` with that entry, if any (the mod's own powers). */
function classNameOf(id, map) {
	const entry = id.split(':')[1];
	return [...map.keys()].find((name) => name.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase() === entry) ?? '';
}

/** title -> the URL of its page on the community wiki, for the titles that have one (following redirects). */
async function wikiPages(titles) {
	const found = new Map();
	try {
		for (let i = 0; i < titles.length; i += 40) {
			const batch = titles.slice(i, i + 40);
			const query = new URLSearchParams({ action: 'query', format: 'json', redirects: '1', titles: batch.map(communityWiki.title).join('|') });
			const response = await fetch(`${communityWiki.api}?${query}`, { headers: { 'User-Agent': 'docs-link-check/1.0' } });
			const { query: result } = await response.json();
			const follow = new Map([...(result.normalized ?? []), ...(result.redirects ?? [])].map((step) => [step.from, step.to]));
			const exists = new Set(Object.values(result.pages).filter((page) => !('missing' in page)).map((page) => page.title));
			for (const title of batch) {
				let page = communityWiki.title(title);
				for (let hops = 0; follow.has(page) && hops < 5; hops++) page = follow.get(page);
				if (exists.has(page)) found.set(title, communityWiki.page(page));
			}
		}
	} catch (error) {
		console.warn(`extract-game-art: couldn't reach the community wiki (${error.message}); keeping the links from last time`);
		return null;
	}
	return found;
}
