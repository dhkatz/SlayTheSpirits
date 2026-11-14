// Hover boxes. Anything with data-tip="<kind>/<slug>" (the inline references from src/lib/refs.mjs, and keywords on
// card faces) shows the matching piece of HTML from /tips/<kind>/<slug>/ beside it, on hover or keyboard focus.
// Each is fetched once, the first time it's needed.

const base = import.meta.env.BASE_URL.replace(/\/$/, '');
const cache = new Map<string, Promise<string>>();
const gap = 10;
/** How long the pointer has to rest on a reference, so sweeping across a row of them doesn't flash every box. */
const delay = 120;
let waiting = 0;

let box: HTMLElement | null = null;
let current: Element | null = null;

const load = (tip: string) => {
	if (!cache.has(tip)) {
		cache.set(
			tip,
			fetch(`${base}/tips/${tip}/`)
				.then((response) => (response.ok ? response.text() : Promise.reject(new Error(String(response.status)))))
				.catch(() => {
					cache.delete(tip); // try again next time
					return '';
				})
		);
	}
	return cache.get(tip)!;
};

function place(anchor: Element) {
	if (!box) return;
	const target = anchor.getBoundingClientRect();
	const size = box.getBoundingClientRect();
	const { clientWidth: width, clientHeight: height } = document.documentElement;
	// Below the reference if it fits, otherwise above; kept inside the window sideways.
	const below = target.bottom + gap + size.height <= height || target.top - gap - size.height < 0;
	const top = below ? target.bottom + gap : target.top - gap - size.height;
	const left = Math.min(Math.max(gap, target.left + target.width / 2 - size.width / 2), width - size.width - gap);
	box.style.transform = `translate(${Math.round(left + window.scrollX)}px, ${Math.round(top + window.scrollY)}px)`;
}

async function show(anchor: Element, wait = 0) {
	const tip = anchor.getAttribute('data-tip');
	if (!tip) return;
	current = anchor;
	clearTimeout(waiting);
	const loaded = load(tip); // start fetching while waiting
	if (wait) await new Promise((resolve) => (waiting = window.setTimeout(resolve, wait)));
	const html = await loaded;
	if (current !== anchor || !html) return; // moved on while it loaded
	if (!box) {
		box = document.createElement('div');
		box.className = 'sts-tooltip not-content';
		box.setAttribute('role', 'tooltip');
		document.body.append(box);
	}
	box.innerHTML = html;
	box.hidden = false;
	place(anchor);
}

function hide(anchor?: Element | null) {
	if (anchor && anchor !== current) return;
	clearTimeout(waiting);
	current = null;
	if (box) box.hidden = true;
}

const tipOf = (target: EventTarget | null) => (target instanceof Element ? target.closest('[data-tip]') : null);

document.addEventListener('mouseover', (event) => {
	const anchor = tipOf(event.target);
	if (anchor && anchor !== current) show(anchor, delay);
	else if (!anchor) hide();
});
document.addEventListener('focusin', (event) => {
	const anchor = tipOf(event.target);
	if (anchor) show(anchor);
});
document.addEventListener('focusout', (event) => hide(tipOf(event.target)));
document.addEventListener('keydown', (event) => event.key === 'Escape' && hide());
window.addEventListener('scroll', () => current && place(current), { passive: true });
