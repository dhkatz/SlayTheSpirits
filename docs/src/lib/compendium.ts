// The model behind <Compendium>: describe how items can be filtered and sorted once, as dimensions, and get the
// controls (facets and sorts) plus the data attributes each item needs. The client script in Compendium.astro
// only ever reads those attributes, so it works for any kind of item.

export interface Dimension<T> {
	/** URL parameter and data attribute name. */
	key: string;
	label: string;
	/** Makes this a filter: the value(s) an item has. */
	facet?: (item: T) => string | readonly string[] | null | undefined;
	/** Facet options in this order; values not listed come after, alphabetically. */
	order?: readonly string[];
	/** A facet option's label (defaults to the value). */
	display?: (value: string) => string;
	/** A facet option's swatch color. */
	color?: (value: string) => string | undefined;
	/** A facet option's icon (a path under public/), shown instead of the swatch. */
	icon?: (value: string) => string | null | undefined;
	/** Makes this a sort: the item's sort key. Ties keep the list's own order. */
	sort?: (item: T) => string | number;
}

export interface FacetOption {
	value: string;
	label: string;
	count: number;
	color?: string;
	icon?: string;
}

export interface Facet {
	key: string;
	label: string;
	options: FacetOption[];
}

export interface Sort {
	key: string;
	label: string;
}

export interface Toggle {
	key: string;
	label: string;
}

export interface CompendiumModel<T> {
	facets: Facet[];
	sorts: Sort[];
	/** Data attributes for an item's element. */
	attributes: (item: T) => Record<string, string>;
}

/** Facet values as they appear in attributes and URLs: "Very Rare" -> "very-rare". */
export const token = (value: string) => value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'none';

const values = <T>(dimension: Dimension<T>, item: T): string[] => {
	const raw = dimension.facet?.(item);
	return raw === null || raw === undefined ? [] : typeof raw === 'string' ? [raw] : [...raw];
};

/**
 * Builds the controls and attributes for a list of items. Facets with fewer than two options are dropped (filtering
 * by a value every item shares does nothing).
 */
export function compendium<T>(items: readonly T[], dimensions: readonly Dimension<T>[], search: (item: T) => string): CompendiumModel<T> {
	const facets = dimensions
		.filter((d) => d.facet)
		.map((d): Facet => {
			const counts = new Map<string, number>();
			for (const item of items) for (const value of values(d, item)) counts.set(value, (counts.get(value) ?? 0) + 1);
			const order = d.order ?? [];
			const known = (v: string) => (order.includes(v) ? order.indexOf(v) : order.length);
			const options = [...counts]
				.sort(([a], [b]) => known(a) - known(b) || a.localeCompare(b, undefined, { numeric: true }))
				.map(([value, count]) => ({
					value: token(value),
					label: d.display?.(value) ?? value,
					count,
					color: d.color?.(value),
					icon: d.icon?.(value) ?? undefined,
				}));
			return { key: d.key, label: d.label, options };
		})
		.filter((facet) => facet.options.length > 1);

	const sorts = dimensions.filter((d) => d.sort).map((d) => ({ key: d.key, label: d.label }));

	const attributes = (item: T) => {
		const attrs: Record<string, string> = { 'data-item': '', 'data-search': search(item).toLowerCase() };
		for (const d of dimensions) {
			if (d.facet) attrs[`data-f-${d.key}`] = values(d, item).map(token).join(' ');
			if (d.sort) attrs[`data-s-${d.key}`] = String(d.sort(item));
		}
		return attrs;
	};

	return { facets, sorts, attributes };
}
