"""
Builds FTP-based training sessions from current exercise-physiology studies as replayable CSV profiles.

    python studien.py <ausgabeordner> [ftp ...]      (default: 150 200 250 W)

Each session is written once per FTP as S<nr>_FTP<ftp>_<name>.csv, the FTP-independent ramp test as
S00_FTP000_Rampentest.csv. Sources and intensities are documented in Programme/LIESMICH.md.
"""
import os
import sys

MIN_WATT = 25   # cockpit minimum
MAX_WATT = 800  # 8008 TRS maximum


def watt(fraction, ftp):
    return min(MAX_WATT, max(MIN_WATT, int(round(fraction * ftp / 5)) * 5))


# Sessions as (number, name, step in s, blocks); a block is (seconds, fraction of FTP). The warm-up and cool-down are part of
# the session so it can be ridden as it is.
WARMUP = [(300, 0.45), (300, 0.55), (180, 0.65)]
COOLDOWN = [(300, 0.50), (300, 0.40)]


def repeat(times, on, off, last_off=True):
    blocks = []
    for i in range(times):
        blocks.append(on)
        if last_off or i < times - 1:
            blocks.append(off)
    return blocks


SESSIONS = [
    # Polarised training: most of the volume clearly below the first threshold (Seiler; Stöggl & Sperlich 2014).
    (1, "Grundlage_60min", 15, [(300, 0.45), (3300, 0.65), (300, 0.50)]),
    # Helgerud et al. 2007: 4 x 4 min at 90-95 % HFmax, 3 min active rest - larger VO2max gain than LSD or threshold.
    (2, "Norwegisch_4x4", 15, WARMUP + repeat(4, (240, 1.08), (180, 0.55), last_off=False) + COOLDOWN),
    # Seiler et al. 2013: 4 x 8 min (2 min rest) gave the largest gains of 4x4 / 4x8 / 4x16 at the highest sustainable
    # effort for that duration.
    (3, "Seiler_4x8", 15, WARMUP + repeat(4, (480, 0.98), (120, 0.50), last_off=False) + COOLDOWN),
    # Rønnestad et al. 2015/2020: 3 series of 13 x (30 s hard / 15 s easy), 3 min between series.
    (4, "Ronnestad_30-15", 15, WARMUP + repeat(3, None, (180, 0.50), last_off=False) + COOLDOWN),
    # Norwegian "double threshold": long sub-threshold intervals, short rests, lactate about 2-3 mmol/l.
    (5, "Unterschwelle_5x8", 15, WARMUP + repeat(5, (480, 0.90), (60, 0.50), last_off=False) + COOLDOWN),
    # Little et al. 2010/2011 (Gibala): low-volume HIIT, 10 x 60 s / 60 s, also studied in untrained and overweight people.
    (6, "Gibala_10x1min", 15, WARMUP + repeat(10, (60, 1.15), (60, 0.45), last_off=False) + COOLDOWN),
    # Gillen et al. 2016: "one minute of intense exercise" - 3 x 20 s sprints in a 10-min session. The brake holds a
    # fixed power, so the sprint is set to 200 % FTP instead of all-out. 5 s steps for the 20 s sprints.
    (7, "Sprint_3x20s", 5, [(120, 0.40)] + repeat(3, (20, 2.00), (120, 0.40)) + [(60, 0.40)]),
]
RONNESTAD_SERIES = repeat(13, (30, 1.25), (15, 0.50))


def expand(blocks):
    out = []
    for b in blocks:
        out.extend(RONNESTAD_SERIES if b is None else [b])
    return out


def rows(step, blocks, ftp):
    result, t = [], 0
    for seconds, fraction in blocks:
        for s in range(0, seconds, step):
            result.append((t + s, watt(fraction, ftp)))
        t += seconds
    result.append((t, result[-1][1]))
    return result


def ramp_rows():
    """Ramp test: 5 min at 100 W, then +20 W per minute up to the brake maximum. Stop when you can't hold it;
    FTP is about 75 % of the best 1-min power (= the last step held for a full minute)."""
    result = [(t, 100) for t in range(0, 300, 15)]
    t, w = 300, 120
    while w <= MAX_WATT:
        result += [(t + s, w) for s in range(0, 60, 15)]
        t, w = t + 60, w + 20
    result.append((t, MAX_WATT))
    return result


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8-sig", newline="\r\n") as f:
        f.write("Sekunden;Soll_W;Puls\n")
        for t, w in data:
            f.write(f"{t};{w};0\n")


out = sys.argv[1]
ftps = [int(a) for a in sys.argv[2:]] or [150, 200, 250]
write(os.path.join(out, "S00_FTP000_Rampentest.csv"), ramp_rows())
for ftp in ftps:
    for no, name, step, blocks in SESSIONS:
        data = rows(step, expand(blocks), ftp)
        write(os.path.join(out, f"S{no:02d}_FTP{ftp}_{name}.csv"), data)
        avg = sum(w for _, w in data[:-1]) / (len(data) - 1)
        print(f"FTP {ftp}  {name:24s} {data[-1][0] / 60:5.1f} min  Ø {avg:3.0f} W  max {max(w for _, w in data)} W")
