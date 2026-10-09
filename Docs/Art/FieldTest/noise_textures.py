import sys, random, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageOps
T, D = sys.argv[1], sys.argv[2]
def tile_noise(size, cells, seed):
    # Value noise: a small random grid, tiled 3x3 and upscaled bicubically, centre crop. Smooth and seamless.
    rnd = random.Random(seed)
    cells = max(2, int(cells))
    base = Image.new("L", (cells, cells)); base.putdata([rnd.randint(0, 255) for _ in range(cells * cells)])
    big = Image.new("L", (cells * 3, cells * 3))
    for x in range(3):
        for y in range(3): big.paste(base, (x * cells, y * cells))
    big = big.resize((size * 3, size * 3), Image.BICUBIC)
    return big.crop((size, size, 2 * size, 2 * size))
def fbm(size, seed):
    n1, n2, n3 = tile_noise(size, 6, seed), tile_noise(size, 16, seed + 1), tile_noise(size, 48, seed + 2)
    return ImageOps.autocontrast(Image.blend(Image.blend(n1, n2, 0.35), n3, 0.15), cutoff=0.5)
fbm(512, 11).save(f"{T}/FX_MacroNoise.png")
n = 256; h = fbm(n, 23); px = h.load(); nm = Image.new("RGB", (n, n)); out = nm.load(); k = 1.6
for y in range(n):
    for x in range(n):
        dx = (px[(x + 1) % n, y] - px[(x - 1) % n, y]) / 255.0 * k
        dy = (px[x, (y + 1) % n] - px[x, (y - 1) % n]) / 255.0 * k
        l = math.sqrt(dx * dx + dy * dy + 1.0)
        out[x, y] = (int((-dx / l * 0.5 + 0.5) * 255), int((-dy / l * 0.5 + 0.5) * 255), int((1 / l * 0.5 + 0.5) * 255))
nm.save(f"{T}/FX_WaterNormal.png")
S = 512; noise = fbm(S, 31)
def rgba(color, alpha): return Image.merge("RGBA", tuple(Image.new("L", (S, S), c) for c in color) + (alpha,))
a = Image.new("L", (S, S), 0); d = ImageDraw.Draw(a); rnd = random.Random(3)
for _ in range(26):
    x = rnd.randint(30, S - 30); w = rnd.randint(4, 18); l = rnd.randint(S // 3, S - 40)
    for y in range(l): d.line((x - w // 2, y, x + w // 2, y), fill=int(170 * (1 - y / l) ** 0.7))
a = ImageChops.multiply(a.filter(ImageFilter.GaussianBlur(4)), noise.point(lambda v: 120 + v // 2))
rgba((32, 30, 28), a).save(f"{D}/DEC_Leak.png")
a = noise.point(lambda v: max(0, min(255, (v - 130) * 4))).filter(ImageFilter.GaussianBlur(2))
rgba((104, 50, 26), a.point(lambda v: min(190, v))).save(f"{D}/DEC_Rust.png")
grad = Image.new("L", (S, S)); grad.putdata([int(255 * (y / S) ** 2.2) for y in range(S) for _ in range(S)])
rgba((40, 36, 30), ImageChops.multiply(grad, noise.point(lambda v: 90 + v * 2 // 3)).point(lambda v: min(190, v))).save(f"{D}/DEC_Dirt.png")
a = Image.new("L", (S, S), 0); d = ImageDraw.Draw(a); rnd = random.Random(9)
def crack(x, y, ang, length, width, depth):
    for _ in range(int(length)):
        nx, ny = x + math.cos(ang) * 6, y + math.sin(ang) * 6
        d.line((x, y, nx, ny), fill=230, width=max(1, int(width)))
        x, y = nx, ny; ang += rnd.uniform(-0.4, 0.4); width *= 0.988
        if depth < 3 and rnd.random() < 0.06: crack(x, y, ang + rnd.choice([-1, 1]) * rnd.uniform(0.5, 1.1), length * 0.4, width * 0.7, depth + 1)
crack(S * 0.08, S * 0.55, -0.15, 78, 5, 0)
rgba((18, 18, 18), a.filter(ImageFilter.GaussianBlur(0.7))).save(f"{D}/DEC_Crack.png")
a = Image.new("L", (S, S), 0); ap = a.load(); np_ = noise.load()
for x in range(S):
    top = int(S * 0.45 + (np_[x, 40] - 128) * 0.35)
    for y in range(S): ap[x, y] = 0 if y < top else int(min(1, (y - top) / 50) * 150)
rgba((20, 22, 24), a.filter(ImageFilter.GaussianBlur(5))).save(f"{D}/DEC_WetEdge.png")
print("ok")
# Puddle shape: a soft blob, irregular edge from the noise, transparent at the border of the quad.
P = 256; pn = fbm(P, 47).load(); pm = Image.new("L", (P, P)); pp = pm.load()
for y in range(P):
    for x in range(P):
        u, v = (x + 0.5) / P * 2 - 1, (y + 0.5) / P * 2 - 1
        r = math.sqrt(u * u + v * v) + (pn[x, y] / 255.0 - 0.5) * 0.55
        pp[x, y] = int(max(0.0, min(1.0, (0.78 - r) / 0.12)) * 255)
pm.filter(ImageFilter.GaussianBlur(1.5)).save(f"{T}/FX_PuddleMask.png")
# Overcast environment cubemap, 6 faces in a row (+X -X +Y -Y +Z -Z): cool grey sky, pale horizon, dark ground.
F = 64; cube = Image.new("RGB", (F * 6, F)); cp = cube.load()
sky, horizon, ground = (158, 166, 174), (176, 180, 182), (46, 46, 44)
def mix(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
for face in range(6):
    for y in range(F):
        for x in range(F):
            u, v = (x + 0.5) / F * 2 - 1, (y + 0.5) / F * 2 - 1
            dy = 1.0 if face == 2 else -1.0 if face == 3 else -v
            l = 1.0 if face in (2, 3) else math.sqrt(1 + u * u + v * v)
            h = dy / l
            cp[face * F + x, y] = mix(horizon, sky, min(1, h * 1.6)) if h >= 0 else mix(horizon, ground, min(1, -h * 5))
cube.save(f"{T}/FX_EnvCube.png")
