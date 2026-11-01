"""Draws each generated profile in yellow over the manual's chart, for a visual check."""
import glob
import statistics
import sys

from PIL import Image, ImageDraw

import trace as T

sys.argv = ["x", "csv"]
src = open("generate.py", encoding="utf-8").read()
exec(src[src.index("CHARTS = ["):src.index("]\n\n\n") + 1])

tiles = []
for no, name, minutes, mx, page, top, mg, wg in CHARTS:
    g = T.load_chart(page, top)
    box = T.plot_area(g)
    sx, sy = T.GRID[mg]
    rows = [tuple(map(int, l.split(";")[:2])) for l in open(glob.glob(f"csv/P{no:02d}_*.csv")[0], encoding="utf-8-sig").read().splitlines()[1:]]
    tile = g.convert("RGB")
    d = ImageDraw.Draw(tile)
    pts = [(box[0] + t / 60 / mg * sx, box[3] - w / wg * sy) for t, w in rows]
    for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
        d.line([(x1, y1), (x2, y1), (x2, y2)], fill=(255, 200, 0), width=2)
    d.text((box[0] + 5, box[1] + 3), f"{no} {name}", fill=(255, 80, 80))
    tiles.append(tile)

cols = 3
w, h = tiles[0].size
sheet = Image.new("RGB", (w * cols, h * ((len(tiles) + cols - 1) // cols)), "white")
for i, tile in enumerate(tiles):
    sheet.paste(tile, ((i % cols) * w, (i // cols) * h))
sheet.save("compare.png")
half = (len(tiles) + 1) // 2 // cols * cols + cols
sheet.crop((0, 0, w * cols, h * (half // cols))).save("compare1.png")
sheet.crop((0, h * (half // cols), w * cols, sheet.height)).save("compare2.png")
print(sheet.size)
