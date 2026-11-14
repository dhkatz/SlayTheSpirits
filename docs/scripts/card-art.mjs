// Tints the card pieces extracted from the game (game-assets/cards/, see extract-game-art.mjs) the way the game does,
// so the docs can stack them into cards. The game tints with shaders/hsv.gdshader; its maths is a 3×3 color matrix,
// applied here per pixel.

import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import sharp from 'sharp';

/**
 * hsv.gdshader as a matrix: to YIQ, rotate the hue by (1 - h) turns, scale saturation (I and Q) by s, scale
 * everything by v, back to RGB. Rows multiply an [r, g, b] column.
 */
export function hsvMatrix({ h, s, v }) {
	const toYiq = [
		[0.2989, 0.587, 0.114],
		[0.5959, -0.2774, -0.3216],
		[0.2115, -0.5229, 0.3114],
	];
	const angle = (1 - h) * 2 * Math.PI;
	const [cos, sin] = [Math.cos(angle), Math.sin(angle)];
	// GLSL's `col *= hue_shift` multiplies by the transpose of the matrix as written in the shader.
	const hue = [
		[1, 0, 0],
		[0, cos, -sin],
		[0, sin, cos],
	];
	const scale = [
		[v, 0, 0],
		[0, v * s, 0],
		[0, 0, v * s],
	];
	return multiply(invert(toYiq), multiply(scale, multiply(hue, toYiq)));
}

const multiply = (a, b) => a.map((row) => b[0].map((_, j) => row.reduce((sum, x, k) => sum + x * b[k][j], 0)));

function invert(m) {
	const [[a, b, c], [d, e, f], [g, h, i]] = m;
	const det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
	return [
		[(e * i - f * h) / det, (c * h - b * i) / det, (b * f - c * e) / det],
		[(f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det],
		[(d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det],
	];
}

/** Where a layer is served from, under public/. */
export const layerPath = ({ piece, material }) => `generated/cards/${piece}${material ? `--${material}` : ''}.webp`;

/**
 * Writes every layer (a game piece, optionally tinted by a material) to public/<layerPath>. Returns problems instead
 * of throwing, so the caller can report them with its others.
 */
export async function bakeLayers(layers, { pieces, publicDir }) {
	const problems = [];
	const materials = JSON.parse(readFileSync(join(pieces, 'materials.json'), 'utf8'));
	mkdirSync(join(publicDir, 'generated/cards'), { recursive: true });

	const unique = new Map(layers.map((layer) => [layerPath(layer), layer]));
	await Promise.all(
		[...unique].map(async ([path, { piece, material }]) => {
			const source = join(pieces, `${piece}.png`);
			if (!existsSync(source)) return problems.push(`card art: no ${piece}.png in game-assets/cards; run npm run extract-game-art`);
			if (material && !materials[material]) return problems.push(`card art: no material ${material}; run npm run extract-game-art`);

			let image = sharp(source).ensureAlpha();
			if (material) {
				const { data, info } = await image.raw().toBuffer({ resolveWithObject: true });
				const m = hsvMatrix(materials[material]);
				for (let p = 0; p < data.length; p += 4) {
					const [r, g, b] = [data[p] / 255, data[p + 1] / 255, data[p + 2] / 255];
					for (let c = 0; c < 3; c++) {
						const value = m[c][0] * r + m[c][1] * g + m[c][2] * b;
						data[p + c] = Math.round(Math.min(1, Math.max(0, value)) * 255);
					}
				}
				image = sharp(data, { raw: info });
			}
			await image.webp({ quality: 90, alphaQuality: 100 }).toFile(join(publicDir, path));
		})
	);
	return problems;
}

/** Where a card's small icon (for use next to its name) is served from, under public/. */
export const iconPath = ({ frame, border, banner }) =>
	`generated/cards/icon--${[frame, border, banner].map((path) => path.split('/').pop().replace(/\.webp$/, '').replace(/--/g, '-')).join('--')}.webp`;

/**
 * Writes a small picture of a blank card for each look: its tinted frame, portrait border and banner, stacked the way
 * CardFace stacks them (positions from scenes/cards/card.tscn, in its 300x422 units), with a plain portrait.
 * `looks` are objects with the layerPath of each piece; bake the layers first.
 */
export async function bakeIcons(looks, { publicDir, height = 128, portraitColor }) {
	const [left, top, width, tall] = [-15, -2, 331, 430]; // the card plus the banner's overhang
	const scale = height / tall;
	const box = (x, y, w, h) => ({ left: Math.round((x - left) * scale), top: Math.round((y - top) * scale), width: Math.round(w * scale), height: Math.round(h * scale) });
	const piece = async (path, x, y, w, h) => {
		const at = box(x, y, w, h);
		return { input: await sharp(join(publicDir, path)).resize(at.width, at.height, { fit: 'fill' }).png().toBuffer(), left: at.left, top: at.top };
	};
	const portrait = box(25, 43, 250, 190);
	const unique = new Map(looks.map((look) => [iconPath(look), look]));
	await Promise.all(
		[...unique].map(async ([path, look]) => {
			const layers = [
				{ input: { create: { width: portrait.width, height: portrait.height, channels: 4, background: portraitColor } }, left: portrait.left, top: portrait.top },
				await piece(look.frame, 0, 0, 300, 422),
				await piece(look.border, 12.5, 47, 275, 210),
				await piece(look.banner, -13, 4, 327, 83),
			];
			await sharp({ create: { width: Math.round(width * scale), height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
				.composite(layers)
				.webp({ quality: 90, alphaQuality: 100 })
				.toFile(join(publicDir, path));
		})
	);
}
