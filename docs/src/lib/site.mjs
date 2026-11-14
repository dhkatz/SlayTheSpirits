// Where the project lives. Shared by astro.config.mjs, the content extractor and the wiki pages.
export const repo = 'https://github.com/dhkatz/SlayTheSpirits';
export const branch = 'main';

/** A file in the repo on GitHub, optionally at a line. */
export const sourceUrl = (path, line) => `${repo}/blob/${branch}/${path}${line ? `#L${line}` : ''}`;
/** GitHub's editor for a file in the repo. */
export const editUrl = (path) => `${repo}/edit/${branch}/${path}`;
/** GitHub's "new file" page, prefilled with a folder and file name. */
export const newFileUrl = (folder, filename) => `${repo}/new/${branch}/${folder}?filename=${encodeURIComponent(filename)}`;

/** The community wiki for the base game, which the docs link to for the game's own keywords and characters. */
export const communityWiki = {
	api: 'https://slaythespire.wiki.gg/api.php',
	/** A page's title there: its Slay the Spire 2 pages live under a "Slay the Spire 2:" prefix. */
	title: (name) => `Slay the Spire 2:${name}`,
	/** The URL of a page, from its full title. */
	page: (title) => `https://slaythespire.wiki.gg/wiki/${encodeURIComponent(title.replace(/ /g, '_')).replace(/%3A/g, ':')}`,
};
