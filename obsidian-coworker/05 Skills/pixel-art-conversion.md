# Skill: Pixel Art Conversion

Turn a high-res or AI-generated "pixel-art-style" image (for example a DALL-E render) into a true, clean, palette-locked pixel-art sprite at a target resolution, with a transparent background. Diffusion models output big, fuzzy, many-coloured images that only look like pixel art. This snaps them down to a real grid and a fixed palette so they can actually drop into a game. Ships with a tested Python script (`pixelify.py`).

## When to Use

- You generated a sprite in DALL-E / Stable Diffusion / Retro Diffusion and it's 1024px with hundreds of colours and soft edges.
- You have any image you want reduced to a clean N by N sprite in a set palette.
- You want to re-index existing art to a specific palette (pass a palette, any size input).

Not for: posing an existing clean sprite. If you already have a tidy sprite and just want a new pose, redraw from it. This tool cleans up messy input, it does not invent animation frames.

## How It Works

1. **Strip the background** to transparency (a solid colour like magenta, or auto-detected from the top-left pixel).
2. **Autocrop** to the subject so it fills the canvas.
3. **Downscale** to the target size (LANCZOS, keeps aspect, centered on a square canvas).
4. **Lock the palette**: snap every pixel to a fixed hex palette (no dither), or let it pick N colours.
5. **Re-apply hard transparency** from the downscaled alpha (threshold, so no soft fringe).
6. **Save** the native-size sprite plus an upscaled nearest-neighbour preview for eyeballing.

Validated by round-tripping a known 32x32, 5-colour sprite up to 320px and back: it returned to a clean 32x32 in the exact 5 colours, pixel counts within 1-2 of the original.

## Requirements

Python 3 and Pillow.

```bash
pip install pillow          # or: pip install pillow --break-system-packages
```

## The Script

Save as `pixelify.py`.

```python
#!/usr/bin/env python3
"""
pixelify.py - turn a high-res / AI "pixel-art-style" image (e.g. a DALL-E
render) into a true, clean, palette-locked pixel-art sprite with transparency.

Pipeline: strip background -> autocrop to the subject -> downscale to the
target resolution -> lock to a fixed palette (or pick N adaptively) ->
re-apply hard transparency -> save the native-size sprite + an upscaled preview.

Examples:
  python pixelify.py in.png --size 32 --palette 150C21 332232 5A2D2A B15B35 EAA44A --bg auto
  python pixelify.py in.png --size 48 --colors 8          # let it pick 8 colours
  python pixelify.py in.png --size 32 --bg FF00FF         # key out a magenta background
"""
import argparse
from PIL import Image


def hex_to_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def strip_background(img, bg, tol):
    """Make pixels near the background colour fully transparent."""
    if bg is None:
        return img
    bgcol = img.getpixel((0, 0))[:3] if bg == 'auto' else hex_to_rgb(bg)
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if abs(r - bgcol[0]) <= tol and abs(g - bgcol[1]) <= tol and abs(b - bgcol[2]) <= tol:
                px[x, y] = (r, g, b, 0)
    return img


def autocrop(img):
    bbox = img.getbbox()  # bounding box of the non-transparent region
    return img.crop(bbox) if bbox else img


def fit(img, size):
    """Downscale to fit a square canvas of side `size`, keeping aspect, centered."""
    w, h = img.size
    scale = size / max(w, h)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    small = img.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(small, ((size - nw) // 2, (size - nh) // 2))
    return canvas


def lock_palette(img, palette=None, colors=None):
    """Snap every pixel to a fixed palette, or pick `colors` adaptively. No dither."""
    rgb = img.convert('RGB')
    if palette:
        rgbs = [hex_to_rgb(hx) for hx in palette]
        # Pad the 256-slot table with the DARKEST palette colour, not black. On a
        # transparent/soft-edged input the transparent RGB is (0,0,0), which bleeds into
        # edges when downscaling; padding with black would add a spurious pure-black colour.
        # Padding with the darkest real colour maps those edge pixels to the outline instead.
        darkest = min(rgbs, key=sum)
        pal_img = Image.new('P', (1, 1))
        flat = []
        for c in rgbs:
            flat += list(c)
        flat += list(darkest) * (256 - len(rgbs))
        pal_img.putpalette(flat)
        q = rgb.quantize(palette=pal_img, dither=Image.Dither.NONE)
    else:
        q = rgb.quantize(colors=colors or 8, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    out = q.convert('RGBA')
    # re-apply crisp transparency from the (downscaled) alpha channel
    out.putalpha(img.split()[3].point(lambda v: 255 if v > 128 else 0))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('input')
    ap.add_argument('--size', type=int, default=32)
    ap.add_argument('--palette', nargs='*', default=None, help='hex colours, space separated')
    ap.add_argument('--colors', type=int, default=None, help='adaptive palette size if no --palette')
    ap.add_argument('--bg', default=None, help="'auto', a hex colour, or omit to keep existing alpha")
    ap.add_argument('--tol', type=int, default=24, help='background match tolerance (0-255)')
    ap.add_argument('--preview', type=int, default=8, help='preview upscale factor (1 = none)')
    ap.add_argument('--out', default=None)
    args = ap.parse_args()

    img = Image.open(args.input).convert('RGBA')
    img = strip_background(img, args.bg, args.tol)
    img = autocrop(img)
    img = fit(img, args.size)
    img = lock_palette(img, args.palette, args.colors)

    out = args.out or args.input.rsplit('.', 1)[0] + f'_px{args.size}.png'
    img.save(out)
    if args.preview > 1:
        img.resize((args.size * args.preview, args.size * args.preview), Image.NEAREST) \
           .save(out.rsplit('.', 1)[0] + '_preview.png')
    print('wrote', out)


if __name__ == '__main__':
    main()
```

## Usage

```bash
# AI render on a magenta background, down to 32x32, locked to the zealot palette
python pixelify.py zealot_singup.png --size 32 --bg FF00FF \
  --palette 150C21 332232 5A2D2A B15B35 EAA44A

# Auto-detect the background from the corner, let it pick 8 colours
python pixelify.py render.png --size 48 --bg auto --colors 8

# Re-index an existing image to a palette, no background removal
python pixelify.py art.png --size 64 --palette 1A1C2C 5D275D B13E53 EF7D57
```

Outputs `<name>_px<size>.png` (the real sprite) and `<name>_px<size>_preview.png` (upscaled to look at).

Knobs worth turning: `--size` (guess the sprite's native grid, usually 16/32/48/64), `--tol` (raise if background flecks survive, lower if it eats the subject's edges), and `--palette` vs `--colors`.

## Get a palette from a reference sprite

To pull the exact colours out of an existing sprite (to reuse as `--palette`):

```python
from PIL import Image
from collections import Counter
im = Image.open('reference.png').convert('RGBA')
for c, n in Counter(p for p in im.getdata() if p[3] > 20).most_common():
    print(f"{c[0]:02X}{c[1]:02X}{c[2]:02X}  x{n}")
```

## Manual cleanup

The script gets you 90 percent there. In Aseprite, fix the last 10 by hand:

- Kill stray single pixels and repair broken outlines.
- Redraw eyes / small features that downscaling smeared.
- Nudge silhouette pixels so the shape reads at size.
- Confirm the palette is exactly your colours (Sprite > Color Mode > Indexed shows the used set).

## Aseprite alternative (no code)

1. Open the render, Sprite > Sprite Size, set to the target (nearest or bilinear).
2. Sprite > Color Mode > Indexed, load your palette, dithering off.
3. Erase the background, clean up by hand.

Good for a one-off. The script wins when you are batching or want repeatable output.

## Tips

- For DALL-E: generate on a flat bright magenta (#FF00FF) background, render as large as you can, and do one pose per image. Consistency across a multi-pose sheet is poor, so single figures downscale cleaner.
- Lock to a palette you already have (extract it with the snippet above) so new art matches old art exactly.
- Only need three sing poses. Mirror SingLeft for SingRight with a horizontal flip.
- Honest take: if you already have a clean base sprite in the palette, hand-posing from it in Aseprite is usually faster and sharper than cleaning up an AI render. Use this skill for turning references and generated art into usable sprites, not as the main pipeline.
