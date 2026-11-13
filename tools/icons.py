"""Generates placeholder power icons (128x128 RGBA PNGs) with no dependencies."""
import math
import struct
import sys
import zlib

SIZE = 128


def png(path, pixels):
    raw = b''.join(b'\x00' + bytes(sum((list(px) for px in row), [])) for row in pixels)

    def chunk(kind, data):
        c = struct.pack('>I', len(data)) + kind + data
        return c + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        f.write(chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 6, 0, 0, 0)))
        f.write(chunk(b'IDAT', zlib.compress(raw, 9)))
        f.write(chunk(b'IEND', b''))


def blank():
    return [[(0, 0, 0, 0) for _ in range(SIZE)] for _ in range(SIZE)]


def blend(img, x, y, color, alpha=1.0):
    if not (0 <= x < SIZE and 0 <= y < SIZE):
        return
    r, g, b, a = img[y][x]
    cr, cg, cb, ca = color
    ca = ca / 255 * alpha
    out_a = ca + a / 255 * (1 - ca)
    if out_a == 0:
        return
    mix = lambda c1, c0: int((c1 * ca + c0 * (a / 255) * (1 - ca)) / out_a)
    img[y][x] = (mix(cr, r), mix(cg, g), mix(cb, b), int(out_a * 255))


def fill(img, inside, color):
    """Anti-aliased fill using 4x4 supersampling of an inside(x, y) predicate."""
    for y in range(SIZE):
        for x in range(SIZE):
            hits = sum(inside(x + (i + 0.5) / 4, y + (j + 0.5) / 4) for i in range(4) for j in range(4))
            if hits:
                blend(img, x, y, color, hits / 16)


def rounded_rect(x0, y0, x1, y1, r):
    def inside(x, y):
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            return False
        cx = min(max(x, x0 + r), x1 - r)
        cy = min(max(y, y0 + r), y1 - r)
        return (x - cx) ** 2 + (y - cy) ** 2 <= r * r
    return inside


def ring(cx, cy, r_out, r_in):
    return lambda x, y: r_in ** 2 <= (x - cx) ** 2 + (y - cy) ** 2 <= r_out ** 2


def circle(cx, cy, r):
    return lambda x, y: (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def outline(pred, width):
    """Points within `width` of the shape but outside it."""
    def inside(x, y):
        if pred(x, y):
            return False
        return any(pred(x + width * math.cos(a), y + width * math.sin(a)) for a in [k * math.pi / 8 for k in range(16)])
    return inside


def mug(liquid, foam, glass_tint):
    img = blank()
    body = rounded_rect(30, 34, 88, 112, 10)
    handle = lambda x, y: ring(90, 72, 22, 12)(x, y) and x > 86
    foam_shape = lambda x, y: any(circle(cx, 36, 12)(x, y) for cx in (36, 52, 68, 82)) and y <= 46
    silhouette = lambda x, y: body(x, y) or handle(x, y) or foam_shape(x, y)
    fill(img, outline(silhouette, 4), (20, 14, 10, 255))
    fill(img, handle, glass_tint)
    fill(img, body, glass_tint)
    fill(img, rounded_rect(36, 46, 82, 106, 6), liquid)
    fill(img, lambda x, y: rounded_rect(36, 46, 82, 106, 6)(x, y) and 40 <= x <= 46, (255, 255, 255, 70))
    for (bx, by, br) in ((50, 90, 3), (64, 76, 2.5), (72, 96, 2), (56, 64, 2)):
        fill(img, circle(bx, by, br), (255, 255, 255, 120))
    fill(img, foam_shape, foam)
    return img


def trapezoid(x0_top, x1_top, x0_bot, x1_bot, y0, y1):
    def inside(x, y):
        if not (y0 <= y <= y1):
            return False
        t = (y - y0) / (y1 - y0)
        return x0_top + (x0_bot - x0_top) * t <= x <= x1_top + (x1_bot - x1_top) * t
    return inside


def glass(body, liquid_shape, liquid, extras=(), glass_tint=(200, 215, 225, 150)):
    """A glass silhouette: dark outline, translucent body, liquid, then any extra shapes (foam, label…)."""
    img = blank()
    parts = [body] + [shape for shape, _ in extras]
    silhouette = lambda x, y: any(p(x, y) for p in parts)
    fill(img, outline(silhouette, 4), (20, 14, 10, 255))
    fill(img, body, glass_tint)
    fill(img, liquid_shape, liquid)
    for shape, color in extras:
        fill(img, shape, color)
    fill(img, lambda x, y: body(x, y) and liquid_shape(x, y) and x < 50 and int(x) % 9 < 3, (255, 255, 255, 50))
    return img


def tab_icons():
    # Water: plain tumbler, a little blue.
    water = glass(trapezoid(32, 96, 40, 88, 26, 112), trapezoid(35, 93, 41, 87, 56, 109), (120, 190, 245, 200))
    # Sip: wine glass (bowl, stem, foot), red.
    bowl = lambda x, y: circle(64, 46, 30)(x, y) and y >= 30
    stem = rounded_rect(60, 72, 68, 104, 2)
    foot = rounded_rect(40, 102, 88, 112, 5)
    sip = glass(lambda x, y: bowl(x, y) or stem(x, y) or foot(x, y),
                lambda x, y: circle(64, 46, 26)(x, y) and y >= 48, (170, 30, 50, 255))
    # Drink: tall highball, amber with ice.
    drink = glass(rounded_rect(38, 18, 90, 114, 6), rounded_rect(42, 44, 86, 110, 4), (225, 150, 40, 230),
                  extras=((rounded_rect(48, 50, 62, 64, 3), (235, 245, 255, 200)),
                          (rounded_rect(64, 60, 78, 74, 3), (235, 245, 255, 200))))
    # Shot: small heavy shot glass, gold.
    shot = glass(trapezoid(34, 94, 42, 86, 50, 112), trapezoid(37, 91, 43, 85, 64, 100), (240, 180, 40, 255),
                 extras=((rounded_rect(42, 100, 86, 112, 4), (200, 215, 225, 230)),))
    # Chug: big stein with lots of foam.
    stein = mug((200, 120, 25, 255), (255, 250, 238, 255), (175, 140, 95, 255))
    # Finish: a bottle.
    bottle_body = lambda x, y: rounded_rect(40, 54, 88, 116, 10)(x, y) or rounded_rect(56, 16, 72, 60, 4)(x, y)         or circle(64, 56, 24)(x, y) and y >= 46
    finish = glass(bottle_body, rounded_rect(43, 70, 85, 113, 8), (70, 130, 50, 255),
                   extras=((rounded_rect(46, 80, 82, 100, 3), (240, 225, 190, 255)),
                           (rounded_rect(54, 10, 74, 22, 3), (200, 60, 40, 255))),
                   glass_tint=(60, 110, 50, 220))
    return {'water': water, 'sip': sip, 'drink': drink, 'shot': shot, 'chug': stein, 'finish': finish}


def main(out_dir):
    png(f'{out_dir}/spirits_tipsy_power.png', mug((231, 160, 40, 255), (255, 248, 232, 255), (190, 205, 215, 255)))
    png(f'{out_dir}/spirits_designated_driver_power.png', mug((110, 190, 245, 255), (210, 236, 255, 255), (190, 205, 215, 255)))
    for kind, img in tab_icons().items():
        png(f'{out_dir}/spirits_{kind}_tab_power.png', img)
    print('wrote icons to', out_dir)


main(sys.argv[1])
