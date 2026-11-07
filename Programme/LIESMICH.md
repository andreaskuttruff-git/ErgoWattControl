# Trainingsprogramme

Alle Programme lassen sich in der App über **☰ → Trainingsprogramm starten …** fahren. Der Dialog listet sie auf und
zeigt zum ausgewählten Programm den Leistungsverlauf (aus den Daten gezeichnet), Dauer, Ø- und Maximalleistung und die Beschreibung; „Start“ (oder ein
Doppelklick) startet es direkt. Während der Fahrt verschiebst du das ganze Profil mit + / − oder dem Mausrad in
5-W-Schritten.

Die Dateien dieses Ordners werden beim Bauen in die exe eingebettet. Beim Start schreibt die App jede fehlende oder
veränderte Datei nach `Programme\` neben der exe – dort geänderte Programme werden also überschrieben. Eigene Programme
unter einem anderen Dateinamen lässt sie in Ruhe.

- **P…** – die Festprogramme aus dem Daum-Handbuch, feste Wattwerte (Abschnitt A)
- **S…** – Programme nach aktueller Trainingswissenschaft, auf deine FTP bezogen (Abschnitt B)

Jedes Programm ist eine JSON-Datei:

```json
{
  "Name": "S02 Norwegisch 4×4 – FTP 200 W",
  "Beschreibung": "4 × 4 min bei 108 % FTP …",
  "Profil": [ [0, 90], [15, 90], … ]   // [Sekunde, Watt] – die Leistung gilt bis zum nächsten Punkt
}
```

## A. Programme aus dem Handbuch (P01–P44)

Die wattgesteuerten Festprogramme des Daum ergo_bike 8008 TRS, nachgebaut aus den Programmgrafiken im
Benutzerhandbuch (S. 9–13). Dateinamen `P<Nr>_<Name>.json`.

| Nr. | Programm | Dauer | max. Watt | Herkunft |
|---|---|---|---|---|
| 1 | Leistungstest WHO 25 Watt | 32 min | 400 | Norm: 25 W, alle 2 min +25 W |
| 2 | Leistungstest BAL 50 Watt | 24 min | 400 | Norm: 50 W, alle 3 min +50 W |
| 3–5 | Intervalltraining Low / Mid / High | 33 / 35 / 38 min | 110 / 160 / 210 | Grafik |
| 6–8 | Pyramide Low / Mid / High | 19 / 43 / 45 min | 115 / 175 / 225 | Grafik |
| 9–11 | The Alpes / Matterhorn / Mount Everest | 23 / 30 / 40 min | 180 / 270 / 300 | Grafik |
| 12–13 | Active Profi Lady / Active Sport Lady | 25 / 22 min | 110 / 90 | Grafik |
| 14–15 | Beginners Training 1 / 2 | 18 / 23 min | 125 / 130 | Grafik |
| 16–17 | Body Watching 1 / 2 | 34 / 53 min | 125 / 100 | Grafik |
| 18–19 | Short 1 / 2 | 20 / 30 min | 100 / 180 | Grafik |
| 20–22 | Fahrtspiel leicht / mittel / schwer | 70 / 84 / 100 min | 400 / 500 / 600 | Grafik |
| 23–25 | Tempotraining 4er Mannschaft leicht / mittel / schwer | 134 min | 360 / 420 / 500 | Grafik, Wechsel nachgebildet |
| 26–28 | Ausdauerbelastung I / II / III | 80 min | 450 / 500 / 550 | Grafik |
| 42–44 | Cool-down leicht / mittel / schwer | 10 / 15 / 20 min | 100 / 100 / 150 | Grafik |

### Genauigkeit

- Die Kurven sind aus den kleinen Grafiken abgelesen (300 dpi, Zeitachse und Watt über die Gitterlinien geeicht),
  alle 15 s ein Wert, auf 5 W gerundet, mindestens 25 W, höchstens die im Handbuch angegebene Maximalleistung.
  Für Programme bis 50 min entspricht ein Bildpunkt etwa 5 s, für die 140-min-Grafiken etwa 15 s.
- **20–22 (Fahrtspiel):** sehr kurze Spitzen sind in der Grafik nur 1–2 Bildpunkte breit; Höhe und Lage stimmen,
  die genaue Dauer einzelner Spitzen ist geschätzt.
- **23–25 (4er Mannschaft):** Die Wechsel sind im Handbuch so dicht gezeichnet, dass sie nicht einzeln lesbar sind.
  Abgelesen sind Führungs- und Windschattenleistung (Ober- und Unterkante des Bandes); der Rhythmus ist nachgebildet:
  30 s Führung, 90 s Windschatten.
- `Erzeugung/Vergleich.png` zeigt jedes Profil (gelb) über der Originalgrafik.

### Nicht enthalten

- **29–38 Pulsprogramme:** Sie geben einen Zielpuls vor, das Cockpit regelt die Leistung nach dem Puls. Die App steuert
  nur Watt; ein festes Wattprofil würde diese Programme nicht nachbilden.
- **39–41 Höhenprofile (Hawaii, Roth, Lanzarote):** Die Grafik enthält nur Höhenmeter über Distanz, keine Koordinaten;
  für „Strecke fahren“ wäre eine GPX-Datei der Originalstrecke besser.

## B. Programme nach aktueller Trainingswissenschaft (S00–S08)

Diese Programme folgen Trainingsformen, deren Wirkung in Studien belegt ist. Die Leistung ist auf deine **FTP** bezogen. FTP heißt Functional Threshold Power: die Leistung, die du etwa eine Stunde halten kannst. Jedes Programm liegt für FTP 150, 200 und 250 W vor (Dateinamen `S<Nr>_FTP<Wert>_<Name>.json`, zum Beispiel `S02_FTP200_Norwegisch_4x4.json`).

### 1. FTP bestimmen: `S00_FTP000_Rampentest.json`

- **Ablauf:** 5 min bei 100 W, danach steigt die Leistung jede Minute um 20 W. Fahr, bis du die Trittfrequenz nicht mehr halten kannst, und brich dann ab.
- **Auswertung:** FTP ≈ **75 % der letzten Stufe, die du eine volle Minute gehalten hast**. Das Ergebnis ist eine Schätzung: Ausdauernde Fahrer schätzt der Test oft zu niedrig, sprintstarke eher zu hoch.
- **Stufe wählen:** Nimm die nächstgelegene FTP-Stufe. Liegt deine FTP dazwischen, kannst du das Profil in der App verschieben oder die Dateien neu erzeugen (siehe unten).

### 2. Die Programme

| Datei | Aufbau | Hauptteil (% FTP) | Ziel | Quelle |
|---|---|---|---|---|
| S01 Grundlage 60 min | 55 min gleichmäßig | 65 % | Grundlagenausdauer; soll den größten Teil des Trainings ausmachen („polarisiertes Training“, ca. 80 % locker) | Seiler 2010; Stöggl & Sperlich 2014 |
| S02 Norwegisch 4×4 | 4 × 4 min, 3 min Pause | 108 % | VO₂max (Herz-Schlagvolumen); in der Studie +7,2 % VO₂max nach 8 Wochen | Helgerud et al. 2007 |
| S03 Seiler 4×8 | 4 × 8 min, 2 min Pause | 98 % | VO₂max und Schwellenleistung; die größten Zuwächse im Vergleich 4×4 / 4×8 / 4×16 | Seiler et al. 2013 |
| S04 Rønnestad 30/15 | 3 Serien × 13 × (30 s / 15 s), 3 min zwischen den Serien | 125 % / 50 % | VO₂max und Leistung über 20 min; bei trainierten Radfahrern besser als 4 × 5 min | Rønnestad et al. 2015, 2020 |
| S05 Unterschwelle 5×8 | 5 × 8 min, 1 min Pause | 90 % | Schwellentraining knapp unter der Laktatschwelle („Norwegian Method“, ca. 2–3 mmol/l): viel Umfang bei wenig Ermüdung | Bakken / Ingebrigtsen-Methode |
| S06 Gibala 10×1 min | 10 × 1 min, 1 min Pause | 115 % | Zeitsparendes HIIT; auch bei Untrainierten und Übergewichtigen untersucht (Fitness, Blutzucker) | Little et al. 2010, 2011 |
| S07 Sprint 3×20 s | 10 min gesamt, 3 × 20 s | 200 % | Minimalprogramm „One minute of intense exercise“ | Gillen et al. 2016 |
| S08 Schwellenblöcke 30 min | 30 min gesamt, 3 × 5 min, 2 min Pause | 92 % | Kraftausdauer („Sweet Spot“) in kurzer Zeit; mit 85–95 RPM knieschonend, steigern über die Blocklänge | Allen & Coggan, Training and Racing with a Power Meter |

Alle Programme außer S07 und S08 enthalten 13 min Aufwärmen und 10 min Ausfahren. S07 hat beides kürzer, wie in der Studie; S08 hat 8 min Einfahren und 3 min Ausfahren, damit es in 30 min passt.

### 3. Hinweise

- **Intensität anpassen:** Die Studien steuern die Intensität meist über Puls oder subjektive Anstrengung, nicht über Watt. Die Prozentwerte sind übliche Umrechnungen.
  - Bei 4×4: Puls am Ende des Intervalls etwa 90–95 % deiner HFmax.
  - Bei 4×8 und 30/15: so hart, dass das letzte Intervall gerade noch gelingt.
  - Ist es zu schwer oder zu leicht, verschiebe das Profil mit + / −.
- **Sprints:** Die Wirbelstrombremse hält eine feste Leistung. „All-out“ ist deshalb nicht möglich, S07 setzt stattdessen 200 % FTP. Fahre die Sprints mit hoher Trittfrequenz.
- **Wochenaufbau:** Ein typischer Aufbau sind 2 harte Einheiten pro Woche (S02–S06) und der Rest locker (S01). Mehr harte Einheiten bringen meist nicht mehr.
- **Gesundheit:** Vor dem Einstieg in hochintensives Training empfehlen die Fachgesellschaften (z. B. ACSM, DGSP) bei Vorerkrankungen, Übergewicht oder längerer Pause einen ärztlichen Check, ggf. mit Belastungs-EKG.

### Quellen

- Helgerud J. et al. (2007): Aerobic high-intensity intervals improve VO2max more than moderate training. *Med Sci Sports Exerc* 39(4). https://pubmed.ncbi.nlm.nih.gov/17414804/
- Seiler S. et al. (2013): Adaptations to aerobic interval training: interactive effects of exercise intensity and total work duration. *Scand J Med Sci Sports* 23(1).
- Rønnestad B. R. et al. (2015): Superior physiological adaptations after a microcycle of short intervals versus long intervals in cyclists. *Scand J Med Sci Sports* 25(2).
- Rønnestad B. R. et al. (2020): Short intervals induce superior training adaptations compared with long intervals in cyclists – an effort-matched approach. *Scand J Med Sci Sports* 30(4).
- Stöggl T., Sperlich B. (2014): Polarized training has greater impact on key endurance variables than threshold, high intensity, or high volume training. *Front Physiol* 5:33.
- Little J. P. et al. (2010/2011): Low-volume high-intensity interval training (J Physiol 2010; J Appl Physiol 2011, Typ-2-Diabetes).
- Gillen J. B. et al. (2016): Twelve weeks of sprint interval training improves indices of cardiometabolic health similar to traditional endurance training despite a five-fold lower time commitment. *PLoS One* 11(4).
- Weston M. et al. (2014): Effects of low-volume HIT on fitness in adults: a meta-analysis. *Sports Med* 44. https://link.springer.com/doi/10.1007/s40279-014-0180-z

## C. Eigene Programme

Eigene Programme gehören nicht in diesen Ordner, sondern nach `CustomPrograms` (unter `Dokumente\ErgoWattControl`, portabel neben der exe). Die App legt dort beim Start das JSON-Schema `Trainingsprogramm.schema.json` sowie `CLAUDE.md` und `AGENTS.md` (gleicher Inhalt aus `Schema/CLAUDE.md`) mit Anleitung für KI-Assistenten ab und beim ersten Mal die Vorlage `Beispiel_Eigenes_Programm.json`. Eigene Programme erscheinen mit ★ oben in der Programmliste; eine fehlerhafte Datei wird mit Grund unter „Nicht lesbar“ genannt.
## Neu erzeugen

Die Profile entstehen in zwei Schritten: Python-Skripte schreiben sie als CSV (`Sekunden;Soll_W;Puls`), das Werkzeug
`Erzeugung/ProgrammeNachJson` macht daraus die JSON-Dateien. Name und Beschreibung kommen aus
`Erzeugung/beschreibungen.json` (Schlüssel ist das Kürzel vor dem ersten `_`, also `P22` oder `S02`).

1. **Handbuch-Programme** (Python 3 mit Pillow, `pdftoppm` aus Poppler), im Ordner mit dem Handbuch:
   ```
   pdftoppm -r 300 -f 11 -l 15 -png 8008TRS-pro-bike.pdf p
   python Programme/Erzeugung/generate.py csv
   python Programme/Erzeugung/compare.py
   ```
   `compare.py` legt jedes Profil über die Originalgrafik (`Erzeugung/Vergleich.png`).
2. **Studien-Programme**, z. B. für eine FTP von 180 W (ohne Angabe: 150, 200 und 250 W):
   ```
   python Programme/Erzeugung/studien.py csv 180
   ```
3. **JSON erzeugen** (.NET 10):
   ```
   dotnet run --project Programme/Erzeugung/ProgrammeNachJson -- csv Programme/Erzeugung/beschreibungen.json Programme
   ```
