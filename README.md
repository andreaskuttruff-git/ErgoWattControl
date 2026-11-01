# ErgoWattControl

Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232, 9600 8N1) – als Ersatz für das Cockpit-Bedienteil und die Originalsoftware ergo_win 2003.

📖 **[Hilfe / Bedienungsanleitung](https://andreaskuttruff-git.github.io/ErgoWattControl/Hilfe.html)** – mit Screenshots; in der App mit F1.

⬇️ **[Download der aktuellen Version](https://github.com/andreaskuttruff-git/ErgoWattControl/releases/latest)** – ZIP entpacken, `ErgoWattControl.exe` starten (braucht die [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)).

## Funktionen

- **Freies Training:** Wattvorgabe in 5-W-Schritten (Tasten ↑↓ + −, Mausrad, Bild ↑↓ ±25 W), Live-Daten von Leistung, RPM, Puls, Geschwindigkeit, Zeit
- **Fahransicht** für einen kleinen Monitor am Rad (1280×1024): Verlaufsgraphen für Leistung/RPM und Puls, erlaubter Drehzahlbereich der Wirbelstrombremse als Band, Pfeile für schneller/langsamer treten
- **Aufzeichnung** als JSON: Art (Training/Strecke/Programm), die Einstellungen beim Start, die Vorgabe (Strecke alle 10 m mit Höhe und Koordinaten bzw. Leistungsverlauf des Programms/Trainings) und eine Zeile Messwerte pro Sekunde. Die Datei wird alle 10 s vollständig neu geschrieben, ein Absturz kostet höchstens diese Sekunden. Ältere CSV-Aufzeichnungen lassen sich weiter wiederholen.
- **Export** (☰ → Aufzeichnung exportieren): FIT für Strava, Garmin Connect, intervals.icu, TrainingPeaks (Strecken als virtuelle Fahrt mit Positionen, sonst Indoor-Training; Garmin FIT SDK) und CSV für Excel
- **Training/Programm wiederholen:** Die Startdialoge zeigen die Einstellungen, die für den Lauf wirken, änderbar nur für diesen Lauf („Als Standard speichern“ übernimmt sie in die `settings.json`). Eine Strecken-Aufzeichnung wird wieder als Strecke gefahren (gespeicherte Strecke, simuliert mit den Einstellungen der Aufzeichnung: Gewichte, Übersetzung, Steigungsfaktor, Widerstände, Wattgrenze). Programme und andere Aufzeichnungen folgen dem Leistungsverlauf – dort wirkt nur die Wattgrenze.
- **Strecke fahren:** GPX-Strecke nachfahren; aus Steigung, Gewicht und virtueller Geschwindigkeit (Trittfrequenz × Gang) wird die Bremsleistung berechnet. Übersetzung Rennrad oder MTB, Steigungsfaktor einstellbar
- Automatisches Verbinden und Neuverbinden nach einem Cockpit-Reset
- **Trainingsprogramme** (JSON mit Beschreibung und Leistungsprofil), fahrbar über „Programm wiederholen“. Sie sind in die exe eingebettet; beim Start schreibt die App fehlende oder veränderte Dateien nach `Programme\` neben der exe (Änderungen dort werden also überschrieben, eigene Programme mit anderem Namen bleiben erhalten). Der Auswahldialog zeichnet zum markierten Programm den Leistungsverlauf und zeigt die Beschreibung (Details und Quellen in `Programme\LIESMICH.md`):
  - `P01`–`P44`: die wattgesteuerten Festprogramme des 8008 TRS aus dem Handbuch (1–28, Cool-down 42–44)
  - `S00`–`S07`: Programme nach aktueller Trainingswissenschaft – Rampentest zur FTP-Bestimmung, Grundlage, 4×4, 4×8, 30/15, Unterschwelle, 10×1 min, 3×20 s, jeweils für FTP 150/200/250 W
- **Hilfe** für Fahrer in einem eigenen Fenster (WebView2): F1 öffnet sie beim Abschnitt zur aktuellen Ansicht bzw. zum offenen Dialog, ☰ → Hilfe am Anfang. `Hilfe.html` und `Hilfe-Bilder\` sind in die exe eingebettet; ohne WebView2-Laufzeit öffnet sich die Hilfe im Browser
- **Demo-Modus** ohne Ergometer: `ErgoWattControl.exe --demo [strecke.gpx]`
- **Start auf dem Hauptbildschirm** statt auf dem Fahrer-Monitor: `ErgoWattControl.exe -main` (kombinierbar mit `-demo`)

## Aufbau des Quellcodes

| Ordner | Inhalt |
|---|---|
| `Cockpit/` | RS232-Protokoll (`DaumCockpit`), Verbindung/Neuverbinden (`ErgoController`), Drehzahlbereich der Bremse, Demo-Fahrer |
| `RideView/` | Fahransicht (`RideForm`), Verlaufsgraph, Mausrad, Zusammenfassung, Monitorauswahl |
| `Recording/` | Aufzeichnung (`JsonRecorder`, Dateiformat `RecordingFile`), Umbenennen/Verwerfen |
| `Route/` | GPX-Strecke, Simulation (Gänge, Steigung, Leistung), Höhenprofil, Streckenauswahl |
| `Replay/` | gespeichertes Training oder Programm wiederholen: Einlesen, Ablauf, Leistungsprofil, Übersicht, Programmauswahl |
| `Settings/` | `settings.json` und Einstellungsdialog |

## Bauen

.NET 10, Windows Forms:

```
dotnet publish -c Release -o publish
```

## Konfiguration

`settings.json` neben der .exe (wird beim ersten Start angelegt): COM-Port, Ablageordner, Monitor der Fahransicht, Gewichte, Übersetzung, Steigungsfaktor sowie die Dateinamen-Muster der Aufzeichnungen:

| Einstellung | Für | Vorgabe |
|---|---|---|
| `FileNameTraining` | freies Training und wiederholte eigene Aufzeichnung | `Training_{Datum}_{Uhrzeit}` |
| `FileNameRoute` | Strecke fahren | `Strecke_{Strecke}_{Datum}_{Uhrzeit}` |
| `FileNameProgram` | Programm wiederholen | `Prog_{Programm}_{Datum}_{Uhrzeit}` |

Platzhalter: `{Datum}`, `{Uhrzeit}`, `{Strecke}` (GPX-Dateiname), `{Training}` (wiederholte Aufzeichnung), `{Programm}` (Programmdatei, z. B. `S02_FTP200_Norwegisch_4x4`). Der Name lässt sich im Start-Dialog von Strecke/Wiederholung/Programm ändern, im freien Training nach „Stop“.
