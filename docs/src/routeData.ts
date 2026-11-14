// Starlight marks a sidebar link as the current page only when its address matches exactly. A wiki entry's page
// (/wiki/cards/cheers/) isn't in the sidebar, so this marks the link it belongs under (Cards) instead.
import { defineRouteMiddleware, type StarlightRouteData } from '@astrojs/starlight/route-data';

type SidebarEntry = StarlightRouteData['sidebar'][number];
type SidebarLink = Extract<SidebarEntry, { type: 'link' }>;

const links = (entries: SidebarEntry[]): SidebarLink[] =>
	entries.flatMap((entry) => (entry.type === 'link' ? [entry] : entry.type === 'group' ? links(entry.entries) : []));

export const onRequest = defineRouteMiddleware((context) => {
	const all = links(context.locals.starlightRoute.sidebar);
	if (all.some((link) => link.isCurrent)) return;
	const path = context.url.pathname;
	// The closest page above this one: the longest link address this page's address starts with.
	const parent = all.filter((link) => link.href.endsWith('/') && path.startsWith(link.href)).sort((a, b) => b.href.length - a.href.length)[0];
	if (parent) parent.isCurrent = true;
});
