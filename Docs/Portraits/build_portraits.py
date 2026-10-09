"""Builds the HUD portraits of the three squad members from their concept sources.

Source: Docs/Portraits/Source/Squad_Portraits.png, one strip of three square panels painted as a set (same framing,
3/4 faces, accent-tinted backgrounds). The script cuts each panel inside its separator, adds a light vignette toward
the HUD charcoal and a tighter face crop for the squad cards. Writes, into Assets/TRACE/UI/Portraits/:
  Portrait_<Name>.png       256 x 256, active operator panel and dialogue
  Portrait_<Name>_Mini.png   72 x 72, squad cards (tighter on the face for small sizes)
  Portrait_Fallback.png     256 x 256, neutral silhouette when a profile has no portrait

Run from the project root:  python3 Docs/Portraits/build_portraits.py
To replace the portraits with final ones, replace the strip (or the boxes below), then re-run.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Docs/Portraits/Source"
OUTPUT = ROOT / "Assets/TRACE/UI/Portraits"
SIZE, MINI = 256, 72
CHARCOAL = (18, 19, 22)

# Panel boxes (left, top, right, bottom) in the strip, inset past the dark separators (columns 722-727, 1445-1448).
STRIP = "Squad_Portraits.png"
PORTRAITS = {
    "Tracewalker": (9, 10, 713, 714),
    "Control": (734, 10, 1438, 714),
    "Support": (1458, 10, 2162, 714),
}
# Mini crops as fractions of the portrait: eyes to chin, centred on each face (Tracewalker's sits left of centre).
MINI_BOXES = {
    "Tracewalker": (0.1, 0.17, 0.62, 0.69),
    "Control": (0.24, 0.2, 0.76, 0.72),
    "Support": (0.24, 0.2, 0.76, 0.72),
}


def vignette(image, strength=0.3):
    """Darkens the edges toward the HUD charcoal so every background ends up in the same place."""
    mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(mask)
    w, h = image.size
    draw.ellipse((-w * 0.18, -h * 0.22, w * 1.08, h * 1.12), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(w * 0.12))
    dark = Image.blend(image, Image.new("RGB", image.size, CHARCOAL), strength)
    return Image.composite(image, dark, mask)


def build(name, box):
    image = Image.open(SOURCE / STRIP).convert("RGB").crop(box).resize((SIZE, SIZE), Image.LANCZOS)
    image = vignette(image)
    image.save(OUTPUT / f"Portrait_{name}.png")
    l, t, r, b = (int(v * SIZE) for v in MINI_BOXES[name])
    mini = image.crop((l, t, r, b)).resize((MINI, MINI), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1, 60, 2))
    mini.save(OUTPUT / f"Portrait_{name}_Mini.png")


def fallback():
    image = Image.new("RGB", (SIZE, SIZE), CHARCOAL)
    draw = ImageDraw.Draw(image)
    grey = (70, 74, 80)
    draw.ellipse((88, 52, 168, 140), fill=grey)
    draw.rounded_rectangle((52, 150, 204, 300), radius=48, fill=grey)
    vignette(image).save(OUTPUT / "Portrait_Fallback.png")


if __name__ == "__main__":
    for name, box in PORTRAITS.items():
        build(name, box)
    fallback()
