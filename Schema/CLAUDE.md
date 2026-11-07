# Eigene Trainingsprogramme für ErgoWattControl

Dieser Ordner (`CustomPrograms`) enthält eigene Trainingsprogramme für **ErgoWattControl**, eine App, die ein
Daum-Ergometer (ergo_bike 8008 TRS) über die Leistung (Watt) steuert. Jede `*.json`-Datei hier – außer
`*.schema.json` – erscheint in der App unter **☰ → Trainingsprogramm starten …** mit ★ oben in der Liste.
Die App liest den Ordner bei jedem Öffnen des Dialogs neu ein; ein Neustart ist nicht nötig.

## Aufgabe: ein Programm erstellen

1. Neue Datei `<Name>.json` in **diesem** Ordner anlegen (nicht im Ordner `Programme` – den stellt die App beim Start
   wieder her). Dateiname ohne Leerzeichen und Umlaute, z. B. `Intervall_6x2min.json`; er erscheint im Dateinamen
   der späteren Aufzeichnung.
2. Inhalt nach `Trainingsprogramm.schema.json` (JSON-Schema, maßgeblich) – Vorlage: `Beispiel_Eigenes_Programm.json`.
3. Datei als UTF-8 speichern.

```json
{
  "$schema": "./Trainingsprogramm.schema.json",
  "Name": "Intervall 6×2 min – FTP 150 W",
  "Beschreibung": "Aufbau in einem Satz.\n\nZiel: …\n\nHinweis: …",
  "Profil": [ [0, 70], [300, 140], [420, 80], [540, 140], [660, 70], [900, 70] ]
}
```

## Regeln für `Profil`

- Liste von Punkten `[Sekunde, Watt]`, beides ganze Zahlen. Die Leistung eines Punkts gilt **bis zum nächsten
  Punkt**; der **letzte Punkt markiert das Ende** (seine Sekunde = Gesamtdauer, Watt = wie der Punkt davor).
- Erster Punkt bei Sekunde **0**, Sekunden **streng steigend**, Watt **0–800**. Das Cockpit regelt ab 25 W; die
  Wattgrenze aus den Einstellungen begrenzt zusätzlich nach oben.
- Watt auf **5 W** runden – die App verstellt in 5-W-Schritten.
- Rampen als Stufen (z. B. alle 15–60 s ein Punkt), keine sekündlichen Punkte nötig.
- Gesamtdauer prüfen: letzte Sekunde / 60 = Minuten.

## Inhaltliche Konventionen

- **Name** (max. 80 Zeichen): kurz und eindeutig; bei FTP-bezogenen Programmen die FTP nennen, z. B. `– FTP 150 W`.
- **Beschreibung**: Aufbau (Blöcke, Dauer, Leistung in Watt und % FTP), Ziel, Hinweise (z. B. Trittfrequenz);
  Absätze mit `\n\n`. Quelle angeben, wenn das Programm einer Trainingsmethode oder Studie folgt.
- Aufbau: Einfahren (5–13 min ansteigend, ca. 45–75 % FTP), Hauptteil, Ausfahren (3–10 min, ca. 40–50 % FTP).
- **FTP** = Leistung, die man etwa eine Stunde halten kann (Rampentest S00 in der App). Übliche Bereiche:
  Grundlage 55–75 %, Sweet Spot 88–94 %, Schwelle 95–105 %, VO₂max 106–120 %.
- Die App kann keinen Puls vorgeben und keine echten Maximalsprints (die Bremse hält eine feste Leistung).
- Beim Fahren verschiebt der Fahrer das ganze Profil mit + / − in 5-W-Schritten – ein Programm muss also nicht
  für jede FTP einzeln existieren; bei Bedarf trotzdem Varianten anlegen (eine Datei je FTP).

## Prüfen

- Gegen das Schema validieren (z. B. VS Code mit der `$schema`-Zeile, oder `python -m jsonschema`).
- In der App: eine fehlerhafte Datei erscheint im Dialog unter „Nicht lesbar“ mit Grund (z. B. „Profil, Punkt 3:
  Sekunde 200 muss größer sein als beim Punkt davor“ oder die Zeile des JSON-Fehlers).
- Der Dialog zeigt zum ausgewählten Programm den Verlauf als Grafik, Dauer, Ø- und Maximalleistung.

## Nicht ändern

`Trainingsprogramm.schema.json`, `CLAUDE.md` und `AGENTS.md` (gleicher Inhalt, für Claude Code bzw. Codex und
andere Assistenten) schreibt die App beim Start neu, falls sie abweichen – Änderungen daran gehen verloren. Alles
andere in diesem Ordner fasst die App nicht an.
