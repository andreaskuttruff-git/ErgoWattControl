"""
Draws ErgoWattControl.ico: the elevation profile of the test route as the route dialog shows it – coloured by gradient,
light-green line, dark background – without text, so it stays recognisable down to 16 px.

    python Setup/make-icon.py [route.gpx]
"""
import math
import os
import sys
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GPX = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "publish", "GPX", "2026-08-23_Test_Anstieg_km13.6-18.6.gpx")
STEP, SMOOTHING = 10.0, 75.0          # as GpxRoute: resample every 10 m, smooth over 75 m
BACK, LINE = (24, 24, 24), (170, 220, 140)


def opaque(alpha, r, g, b):
    """The route profile's gradient colours, pre-blended onto the dark background (RouteProfile.GradeClasses)."""
    return tuple((c * alpha + 24 * (255 - alpha)) // 255 for c in (r, g, b))


CLASSES = [(2, opaque(150, 70, 120, 70)), (4, opaque(170, 140, 165, 55)), (6, opaque(180, 205, 175, 40)),
           (8, opaque(190, 230, 120, 30)), (math.inf, opaque(200, 210, 50, 40))]


def profile(path):
    points = []
    for e in ET.parse(path).iter():
        if e.tag.endswith("trkpt") or e.tag.endswith("rtept"):
            ele = next((c for c in e if c.tag.endswith("ele")), None)
            if ele is not None:
                points.append((float(e.get("lat")), float(e.get("lon")), float(ele.text)))
    dist = [0.0]
    for (a, b, _), (c, d, _) in zip(points, points[1:]):
        p1, p2 = math.radians(a), math.radians(c)
        h = math.sin((p2 - p1) / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(math.radians(d - b) / 2) ** 2
        dist.append(dist[-1] + 2 * 6371000 * math.asin(math.sqrt(h)))
    raw, j = [], 0
    for i in range(int(dist[-1] / STEP) + 1):
        x = i * STEP
        while j < len(points) - 2 and dist[j + 1] < x:
            j += 1
        span = dist[j + 1] - dist[j]
        f = min(max((x - dist[j]) / span, 0), 1) if span > 0 else 0
        raw.append(points[j][2] + f * (points[j + 1][2] - points[j][2]))
    half = int(SMOOTHING / STEP / 2)
    return [sum(raw[max(0, i - half):i + half + 1]) / len(raw[max(0, i - half):i + half + 1]) for i in range(len(raw))]


def draw(elevation, size=1024):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=size // 8, fill=BACK)
    lo, hi = min(elevation), max(elevation)
    left, right, top, bottom = size * 0.06, size * 0.94, size * 0.14, size * 0.86
    n = len(elevation) - 1
    pts = [(left + i / n * (right - left), bottom - (e - lo) / (hi - lo) * (bottom - top) * 0.85 - (bottom - top) * 0.08)
           for i, e in enumerate(elevation)]
    for i in range(n):
        grade = (elevation[i + 1] - elevation[i]) / STEP * 100
        color = next(c for upto, c in CLASSES if grade < upto)
        (x1, y1), (x2, y2) = pts[i], pts[i + 1]
        d.polygon([(x1, bottom), (x1, y1), (x2 + 1, y2), (x2 + 1, bottom)], fill=color)
    d.line(pts, fill=LINE, width=max(2, size // 40), joint="curve")
    return img


big = draw(profile(GPX))
sizes = [16, 24, 32, 48, 64, 128, 256]
icon = os.path.join(ROOT, "ErgoWattControl.ico")
# Classic bitmap entries instead of PNG: the setup window loads its icon with LoadImage, which shows PNG entries as the
# generic window icon.
big.resize((256, 256), Image.LANCZOS).save(icon, sizes=[(s, s) for s in sizes], bitmap_format="bmp")
print(icon)
