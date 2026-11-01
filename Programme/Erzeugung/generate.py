"""Builds replayable CSV profiles of the Daum 8008 TRS watt programs from the manual's charts."""
import os
import statistics
import sys

import trace as T

OUT = sys.argv[1]
STEP = 15          # seconds between profile rows
MIN_WATT = 25      # cockpit minimum

# (no, name, minutes, max watt, page, chart top in preview px, minutes per vertical grid line, watts per horizontal line)
CHARTS = [
    (3, "Intervalltraining Low", 33, 110, 11, 705, 10, 100),
    (4, "Intervalltraining Mid", 35, 160, 11, 798, 10, 100),
    (5, "Intervalltraining High", 38, 210, 11, 893, 10, 100),
    (6, "Pyramide Low", 19, 115, 11, 987, 10, 100),
    (7, "Pyramide Mid", 43, 175, 12, 118, 10, 100),
    (8, "Pyramide High", 45, 225, 12, 212, 10, 100),
    (9, "The Alpes", 23, 180, 12, 306, 10, 100),
    (10, "Matterhorn", 30, 270, 12, 400, 10, 100),
    (11, "Mount Everest", 40, 300, 12, 497, 10, 100),
    (12, "Active Profi Lady", 25, 110, 12, 594, 10, 100),
    (13, "Active Sport Lady", 22, 90, 12, 690, 10, 100),
    (14, "Beginners Training 1", 18, 125, 12, 786, 10, 100),
    (15, "Beginners Training 2", 23, 130, 12, 880, 10, 100),
    (16, "Body Watching 1", 34, 125, 12, 977, 10, 100),
    (17, "Body Watching 2", 53, 100, 13, 118, 10, 100),
    (18, "Short 1", 20, 100, 13, 214, 10, 100),
    (19, "Short 2", 30, 180, 13, 310, 10, 100),
    (20, "leichtes Fahrtspiel", 70, 400, 13, 421, 20, 100),
    (21, "mittleres Fahrtspiel", 84, 500, 13, 516, 20, 100),
    (22, "schweres Fahrtspiel", 100, 600, 13, 611, 20, 100),
    (23, "Tempotraining 4er Mannschaft leicht", 134, 360, 13, 706, 20, 100),
    (24, "Tempotraining 4er Mannschaft mittel", 134, 420, 13, 801, 20, 100),
    (25, "Tempotraining 4er Mannschaft schwer", 134, 500, 13, 896, 20, 100),
    (26, "Ausdauerbelastung I", 80, 450, 14, 118, 20, 100),
    (27, "Ausdauerbelastung II", 80, 500, 14, 212, 20, 100),
    (28, "Ausdauerbelastung III", 80, 550, 14, 306, 20, 100),
    (42, "Cool-down leicht", 10, 100, 15, 813, 5, 25),
    (43, "Cool-down mittel", 15, 100, 15, 904, 5, 25),
    (44, "Cool-down schwer", 20, 150, 15, 995, 5, 25),
]


def axis_trace(page, top, minutes_per_grid, watts_per_grid):
    """Curve in minutes/watts: 0 at the plot's left/bottom edge, grid spacing from the median line distance."""
    g = T.load_chart(page, top)
    box = T.plot_area(g)
    sx, sy = T.GRID[minutes_per_grid]
    pts = []
    for x, y in T.curve(g, box):
        if y is not None:
            t, w = (x - box[0]) / sx * minutes_per_grid * 60, (box[3] - y) / sy * watts_per_grid
            if w >= 20:  # below that it is the white line under the plot, the programs never go under 25 W
                pts.append((t, w))
    return pts


def watt(w, max_watt):
    """5 W steps between the cockpit minimum and the program's stated maximum."""
    return min(max_watt, max(MIN_WATT, int(round(w / 5)) * 5))


def profile(pts, minutes, max_watt):
    """
    Resamples every STEP seconds (median, 5 W steps). Both axes come from the grid lines; the profile is cut at
    (or held until) the stated duration and capped at the stated maximum.
    """
    rows = []
    t = 0
    while t <= minutes * 60:
        window = [w for s, w in pts if abs(s - t) <= STEP / 2] or [min(pts, key=lambda p: abs(p[0] - t))[1]]
        rows.append((t, watt(statistics.median(window), max_watt)))
        t += STEP
    return rows, pts[-1][0] / 60, max(w for _, w in pts)


TEAM_PERIOD = 120  # 4-man team: 30 s in the lead, 90 s in the slipstream
TEAM_LEAD = 30


def team_profile(page, top, minutes_per_grid, watts_per_grid, minutes, max_watt):
    """
    The tempo programs are drawn as a dense hatch between a lead and a slipstream level; the single changes can't be
    read. Per 2-min period the upper and lower edge of the hatch give both levels, laid out as 30 s lead + 90 s behind.
    """
    g = T.load_chart(page, top)
    box = T.plot_area(g)
    sx, sy = T.GRID[minutes_per_grid]
    cols = [((x - box[0]) / sx * minutes_per_grid * 60, (box[3] - e[0]) / sy * watts_per_grid, (box[3] - e[1]) / sy * watts_per_grid)
            for x, e in T.envelope(g, box) if e is not None]
    cols = [c for c in cols if c[2] >= 20]
    rows = []
    for start in range(0, minutes * 60 + 1, TEAM_PERIOD):
        period = [c for c in cols if start <= c[0] < start + TEAM_PERIOD] or [min(cols, key=lambda c: abs(c[0] - start))]
        # The hatch leaves gaps, so the edges are the extremes of the period, not its median.
        high = max(c[1] for c in period)
        low = min(c[2] for c in period)
        for t in range(start, min(start + TEAM_PERIOD, minutes * 60 + 1), STEP):
            if high - low < 40:  # warm-up ramp: a plain line, no hatch
                near = min(cols, key=lambda c: abs(c[0] - t))
                w = (near[1] + near[2]) / 2
            else:
                w = high if t - start < TEAM_LEAD else low
            rows.append((t, watt(w, max_watt)))
    return rows, cols[-1][0] / 60, max(c[1] for c in cols)


def stages(start, step, minutes_per_stage, top):
    """Exact staircase of the standardised tests (WHO, BAL)."""
    rows, t, w = [], 0, start
    while w <= top:
        for s in range(0, minutes_per_stage * 60, STEP):
            rows.append((t + s, w))
        t += minutes_per_stage * 60
        w += step
    rows.append((t, top))
    return rows


def write(no, name, rows):
    path = os.path.join(OUT, f"P{no:02d}_{name.replace(' ', '_')}.csv")
    with open(path, "w", encoding="utf-8-sig", newline="\r\n") as f:
        f.write("Sekunden;Soll_W;Puls\n")
        for t, w in rows:
            f.write(f"{t};{w};0\n")
    return path


os.makedirs(OUT, exist_ok=True)
summary = []
for no, name, start, step, per, top in [(1, "Leistungstest WHO 25 Watt", 25, 25, 2, 400), (2, "Leistungstest BAL 50 Watt", 50, 50, 3, 400)]:
    rows = stages(start, step, per, top)
    write(no, name, rows)
    summary.append((no, name, rows[-1][0] / 60, max(w for _, w in rows), "Norm", ""))
for no, name, minutes, max_watt, page, top, mg, wg in CHARTS:
    if 23 <= no <= 25:
        rows, traced_end, traced_max = team_profile(page, top, mg, wg, minutes, max_watt)
    else:
        pts = axis_trace(page, top, mg, wg)
        rows, traced_end, traced_max = profile(pts, minutes, max_watt)
    write(no, name, rows)
    summary.append((no, name, minutes, max_watt, f"Grafik: Ende {traced_end:.1f} min, Max {traced_max:.0f} W", ""))
for no, name, minutes, mx, how, _ in summary:
    print(f"{no:2d} {name:38s} {minutes:6.1f} min {mx:4d} W  {how}")
