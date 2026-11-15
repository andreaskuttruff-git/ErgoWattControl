# Custom training programs for ErgoWattControl

This folder (`CustomPrograms`) holds custom training programs for **ErgoWattControl**, an app that controls a Daum
ergometer (ergo_bike 8008 TRS) by power (watts). Every `*.json` file here – except `*.schema.json` – appears in the
app under **☰ → Start a training program …** (German: *Trainingsprogramm starten*) with ★ at the top of the list.
The app reads the folder again each time the dialog opens; no restart is needed.

**Language:** answer the rider in the language they write in. Write `Name` and `Description` in that language; if
they ask for it (or the app is used in both languages), write both German and English – see below.

## Task: create a program

1. Create a new file `<name>.json` in **this** folder (not in the folder `Programs` – the app restores that one at
   start). File name without spaces and umlauts, e.g. `Intervals_6x2min.json`; it becomes part of the file name of
   the recordings later.
2. Content according to `TrainingProgram.schema.json` (JSON schema, authoritative) – template:
   `Example_Custom_Program.json`.
3. Save the file as UTF-8.

```json
{
  "$schema": "./TrainingProgram.schema.json",
  "Name": "Intervals 6×2 min – FTP 150 W",
  "Description": "Structure in one sentence.\n\nGoal: …\n\nNote: …",
  "Profile": [ [0, 70], [300, 140], [420, 80], [540, 140], [660, 70], [900, 70] ]
}
```

`Name` and `Description` are either **one text** (used in every language) or **one text per language** (two-letter
codes) – the app then shows the language of its user interface, otherwise English, otherwise German:

```json
  "Name": { "de": "Intervall 6×2 min", "en": "Intervals 6×2 min" },
  "Description": { "de": "Aufbau …", "en": "Structure …" },
```

## Rules for `Profile`

- A list of points `[second, watts]`, both integers. The power of a point holds **until the next point**; the **last
  point marks the end** (its second = total duration, watts = as the point before).
- First point at second **0**, seconds **strictly increasing**, watts **0–800**. The cockpit controls from 25 W; the
  power limit from the settings caps it as well.
- Round watts to **5 W** – the app changes power in steps of 5 W.
- Ramps as steps (e.g. one point every 15–60 s); no points every second are needed.
- Check the total duration: last second / 60 = minutes.

## Conventions for the content

- **Name** (max. 80 characters): short and unique; for FTP-based programs state the FTP, e.g. `– FTP 150 W`.
- **Description**: structure (blocks, duration, power in watts and % FTP), goal, hints (e.g. cadence); paragraphs
  with `\n\n`. Give the source if the program follows a training method or study.
- Structure: warm-up (5–13 min rising, about 45–75 % FTP), main part, cool-down (3–10 min, about 40–50 % FTP).
- **FTP** = the power one can hold for about an hour (ramp test S00 in the app). Usual ranges: endurance 55–75 %,
  sweet spot 88–94 %, threshold 95–105 %, VO₂max 106–120 %.
- The app cannot target a heart rate and cannot do true all-out sprints (the brake holds a fixed power).
- While riding, the rider shifts the whole profile with + / − in steps of 5 W – so a program doesn’t have to exist
  for every FTP; create variants anyway if wanted (one file per FTP).

## Checking

- Validate against the schema (e.g. VS Code with the `$schema` line, or `python -m jsonschema`).
- In the app: a faulty file appears in the dialog under “Not readable” (German: *Nicht lesbar*) with the reason (e.g.
  “Profile, point 3: second 200 must be greater than at the point before” or the line of the JSON error).
- For the selected program the dialog shows the profile as a chart, duration, average and maximum power.

## Older files

The app still reads programs with the fields `Beschreibung` and `Profil` (up to ErgoWattControl 1.5); write new
programs with `Description` and `Profile`.

## Don’t change

The app rewrites `TrainingProgram.schema.json`, `CLAUDE.md` and `AGENTS.md` (same content, for Claude Code and for
Codex and other assistants) at start if they differ – changes to them are lost. It never touches anything else in
this folder.
