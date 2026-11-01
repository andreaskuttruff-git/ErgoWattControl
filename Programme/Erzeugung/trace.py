"""Traces the white power curve out of a Daum manual chart (rendered at 300 dpi)."""
from PIL import Image, ImageFilter

WHITE = 200
DARK = 120


def load_chart(page, top_preview):
    """Crops the chart whose box starts at preview-y `top_preview` (827 px wide preview, factor 3)."""
    im = Image.open(f"p-{page}.png").convert("L")
    return im.crop((340, top_preview * 3 - 15, 1000, top_preview * 3 + 255))


def plot_area(g):
    """Bounding box of the dark plot region (columns/rows that are mostly dark or white, not paper)."""
    w, h = g.size
    px = g.load()
    def dark_ratio_col(x):
        return sum(1 for y in range(h) if px[x, y] < DARK) / h
    # Low thresholds: in the dense tempo programs the white curve covers much of the plot.
    x0, x1 = longest_run([dark_ratio_col(x) > 0.2 for x in range(w)])
    def dark_ratio_row(y):
        return sum(1 for x in range(x0, x1) if px[x, y] < DARK) / (x1 - x0)
    y0, _ = longest_run([dark_ratio_row(y) > 0.12 for y in range(h)])
    # All plots are 161 px high at 300 dpi; the bottom edge is less reliable (axis labels, curve on the baseline).
    return x0, y0, x1, y0 + PLOT_HEIGHT


PLOT_HEIGHT = 161


def longest_run(flags, gap=6):
    """(first, last) index of the longest stretch of True, bridging short gaps (white grid lines cross the plot)."""
    flags = list(flags)
    # fill gaps of up to `gap` False between True values
    i = 0
    while i < len(flags):
        if not flags[i]:
            j = i
            while j < len(flags) and not flags[j]:
                j += 1
            if 0 < i and j < len(flags) and j - i <= gap:
                for k in range(i, j):
                    flags[k] = True
            i = j
        else:
            i += 1
    best, start = (0, 0), None
    for i, f in enumerate(flags + [False]):
        if f and start is None:
            start = i
        elif not f and start is not None:
            if i - 1 - start > best[1] - best[0]:
                best = (start, i - 1)
            start = None
    return best


def gridlines(g, box):
    """Thin white lines running through the whole plot: vertical (x positions) and horizontal (y positions)."""
    x0, y0, x1, y1 = box
    px = g.load()
    def runs(values):
        out, start = [], None
        for i, v in enumerate(values + [False]):
            if v and start is None:
                start = i
            elif not v and start is not None:
                out.append((start + i - 1) / 2)
                start = None
        return out
    vcols = [sum(1 for y in range(y0, y1) if px[x, y] > WHITE) / (y1 - y0) > 0.7 for x in range(x0, x1)]
    hrows = [sum(1 for x in range(x0, x1) if px[x, y] > WHITE) / (x1 - x0) > 0.7 for y in range(y0, y1)]
    return [x0 + v for v in runs(vcols)], [y0 + v for v in runs(hrows)]


# Grid spacing in px at 300 dpi per chart type (keyed by minutes per vertical grid line), measured on charts without
# a dense curve (1, 20, 42). Dense curves hide grid lines or add false ones, so the spacing is not measured per chart.
GRID = {10: (100.1, 34.2), 20: (73.9, 22.5), 5: (103.6, 22.7)}


CURVE = 6  # line thickness in px; a taller white run in one column is a jump


def envelope(g, box):
    """Top and bottom y of the white curve per column (None where nothing), for the densely hatched programs."""
    x0, y0, x1, y1 = box
    mask = g.crop(box).point(lambda v: 255 if v > WHITE else 0)
    opened = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    px = opened.load()
    w, h = opened.size
    out = []
    for x in range(w):
        hits = [y for y in range(h) if px[x, y]]
        out.append((x0 + x, (y0 + min(hits) + CURVE / 2, y0 + max(hits) - CURVE / 2) if hits else None))
    return out


def curve(g, box):
    """
    y of the thick curve per column (None where nothing): grid lines (1-2 px) vanish under a 3x3 opening, the curve is ~5 px.
    In a column with a jump the curve covers the whole height; the value there is the end farther from the previous
    column, i.e. where the jump goes, so narrow spikes keep their height instead of being averaged away.
    """
    x0, y0, x1, y1 = box
    mask = g.crop(box).point(lambda v: 255 if v > WHITE else 0)
    opened = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    px = opened.load()
    w, h = opened.size
    ys, previous = [], None
    for x in range(w):
        hits = [y for y in range(h) if px[x, y]]
        if not hits:
            ys.append(None)
            continue
        top, bottom = min(hits), max(hits)
        if bottom - top <= CURVE or previous is None:
            y = (top + bottom) / 2
        else:
            y = top + CURVE / 2 if abs(top - previous) > abs(bottom - previous) else bottom - CURVE / 2
        ys.append(y0 + y)
        previous = y
    return [(x0 + x, y) for x, y in zip(range(w), ys)]


def trace(page, top, minutes_per_grid, watts_per_grid):
    g = load_chart(page, top)
    box = plot_area(g)
    vx, hy = gridlines(g, box)
    # x: plot left edge is 0 min, vertical lines every `minutes_per_grid`
    spacing_x = (vx[-1] - vx[0]) / (len(vx) - 1)
    # y: horizontal lines every `watts_per_grid`, the lowest one is the first grid value
    hy = sorted(hy, reverse=True)
    spacing_y = (hy[0] - hy[-1]) / (len(hy) - 1)
    base_y = hy[0] + spacing_y  # 0 W
    pts = []
    for x, y in curve(g, box):
        if y is None:
            continue
        t = (x - box[0]) / spacing_x * minutes_per_grid * 60
        w = (base_y - y) / spacing_y * watts_per_grid
        pts.append((t, w))
    return box, vx, hy, pts


if __name__ == "__main__":
    import sys
    page, top = int(sys.argv[1]), int(sys.argv[2])
    box, vx, hy, pts = trace(page, top, 10, 100)
    print("box", box, "vx", vx, "hy", hy)
    for t, w in pts[:: max(1, len(pts) // 40)]:
        print(f"{t/60:5.1f} min {w:5.0f} W")
    print("end", pts[-1][0] / 60, "max", max(w for _, w in pts))
