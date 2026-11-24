Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232).

📖 **[Hilfe / Bedienungsanleitung](https://andreaskuttruff-git.github.io/ErgoWattControl/Help.de.html)** · [Help (English)](https://andreaskuttruff-git.github.io/ErgoWattControl/Help.en.html) – in der App mit F1.

### Neu in v2.3.0
- **Dunkle Meldungen:** Rückfragen, Warnungen und Fehlermeldungen (z. B. „Jetzt neu starten?“ nach einem Sprachwechsel) erscheinen jetzt im dunklen Stil der App statt als weißes Windows-Fenster – mit Windows-Symbol und -Ton; lange Pfade werden umbrochen.
- **Runde Eingabefelder:** Text-, Zahlen- und Listenfelder haben abgerundete Rahmen wie die Knöpfe (hell, solange das Feld aktiv ist). Text, Cursor und die Markierung in Listen haben etwas Abstand zum Rahmen. Auswahlfelder bleiben eckig.
- **Größe der Fahransicht:** Die Standardeinstellung heißt jetzt **„Kleinster Monitor (B × H) – Standard“** und zeigt die Größe, die die Fahransicht damit bekommt – im Demo-Modus 1280 × 1024. Das Auswahlfeld ist breit genug für den ganzen Eintrag.

### Neu in v2.2.1
- **Freies Training:** Die Aufzeichnung pausiert jetzt auch hier, solange nicht getreten wird – wie schon bei Strecken, Aufzeichnungen und Programmen. Die Zeit läuft nur beim Treten, Pausen fehlen in der Aufzeichnung (und im FIT-Export).

### Neu in v2.2.0
- **Kürzlich gefahren:** Im Menü ☰ steht unter „Strecke fahren …“, „Aufzeichnung nachfahren …“ und „Trainingsprogramm starten …“ je ein Punkt **Kürzlich** mit den letzten drei dort gestarteten Einträgen. Ein Klick öffnet den gewohnten Dialog mit dem Eintrag schon ausgewählt – Wattgrenze und Leistungsoffset lassen sich vor „Start“ noch prüfen. Gelöschte oder verschobene Dateien werden nicht angezeigt.
- **Info:** Neue Links **Log öffnen** (öffnet `ErgoWattControl.log` in Notepad++, falls installiert, sonst im Editor) und **Ordner** (öffnet den Ordner mit Log und `settings.json` im Explorer).

### Neu in v2.1.1
- **Startdialog:** Der Knopf **Standard** ist jetzt klein und steht direkt hinter dem „W“ der Wattgrenze.
- **Intern:** GitHub-Workflows auf aktuelle Actions (Node 24) umgestellt, Hinweise der Testanalyse behoben.

### Neu in v2.1.0
- **Leistungsoffset beim Start:** In „Trainingsprogramm starten“ und „Aufzeichnung nachfahren“ lässt sich das ganze Leistungsprofil für diesen Lauf in 5-W-Schritten verschieben – nach unten höchstens bis der tiefste Punkt bei 0 W liegt, nach oben höchstens bis der höchste die Wattgrenze erreicht. Die Vorschau zeigt es gleich verschoben; in der Fahrt verschieben + / − und das Mausrad von dort aus weiter. Der Offset gilt nur für diesen Lauf.
- **Profilgrafik:** Ein nach oben verschobenes Profil wird nicht mehr oben flach abgeschnitten gezeichnet – die Skala wächst mit.
- **„Als Standard speichern“ heißt jetzt „Standard“** und steht hinter der Wattgrenze; die Beschreibung des Programms bekommt dafür mehr Platz.
- **Hochladen zu Garmin Connect / Strava:** Alle Knöpfe stehen jetzt unter der Seite, dazu ein eigener **Schließen**-Knopf (auch Esc).
- **Export:** „Ordner öffnen“ wird bei größerer Windows-Skalierung nicht mehr abgeschnitten.
- **Dialoge:** Etwas mehr Abstand unter den Knöpfen unten; ausgegraute Knöpfe (z. B. „Start“ ohne Auswahl) haben einen sichtbaren Rahmen und eine deutlich dunklere Schrift.
- **Vorschau – noch nicht getestet:** Neuer Gerätetyp **Bluetooth-Smart-Trainer (FTMS, z. B. ErgoBridge)** unter ☰ → Einstellungen → Gerät. Damit soll die App das Ergometer künftig über eine ESP32-Bluetooth-Brücke statt über das RS232-Kabel steuern – oder einen anderen FTMS-Trainer mit Zielleistung. Mit echter Bluetooth-Hardware ist das noch nicht ausprobiert; für das Daum am Kabel bleibt „Daum ergo_bike (RS232)“ die richtige Wahl.

### Neu in v2.0.3
- **Listen scrollen nicht mehr mit der Maus mit:** Manche Maus- oder Touchpad-Treiber (z. B. Synaptics) melden beim bloßen Bewegen der Maus ein seitliches Mausrad – Windows-Listen scrollen darauf senkrecht. Die Listen für Programme, Aufzeichnungen, Strecken und Export ignorieren das seitliche Rad jetzt; lange Namen lassen sich weiter mit der Scrollleiste verschieben.

### Neu in v2.0.2
- **Programm- und Aufzeichnungsauswahl:** Die Liste scrollt nicht mehr von selbst mit, wenn man nach dem Anklicken eines Eintrags die Maus darüber bewegt (trat vor allem per Remotedesktop auf). Gilt auch für die Liste im Export.
- **Log beim Start ohne Cockpit:** Antwortet das Cockpit beim Start nicht (z. B. im Schlafmodus), steht das jetzt einmal in `ErgoWattControl.log` – und sobald die Verbindung steht, auch nach wie vielen Sekunden.

### Neu in v2.0.1
- **Deutsch und Englisch:** Die ganze App gibt es jetzt auf Deutsch und Englisch – Oberfläche, Meldungen, Hilfe (F1), Programmbeschreibungen und Setup. Umschalten unter **☰ → Einstellungen → Sprache / Language** (Automatisch wie Windows, Deutsch, English); die App bietet den Neustart gleich an. Zahlen und Datum folgen weiter den Windows-Einstellungen.
- **Größe der Fahransicht** in den Einstellungen wählbar: wie bisher (Standard), ganzer Monitor oder eine feste Größe wie 1280 × 1024 oder 1920 × 1080.
- **Lauf wird immer gespeichert:** Ist der Ablageordner nicht beschreibbar (Netzlaufwerk oder USB-Stick fehlt), landet die Aufnahme im Standardordner bzw. unter `%LocalAppData%\ErgoWattControl\Recordings`; Export und „Aufzeichnung nachfahren“ finden sie dort. Geht gar nichts, kommt nur eine Meldung statt einer pro Sekunde.
- **Hilfe:** jetzt `Help.de.html` und `Help.en.html`; die alte Adresse `Hilfe.html` leitet weiter.
- **Intern:** Trainingslogik, Fahrwerte und Geräteschicht neu aufgeteilt (Vorbereitung für weitere Ergometer), über 190 Unittests einschließlich Pseudolokalisierung.

**Wichtig – geänderte Dateiformate (deshalb v2):**
- Aufzeichnungen, Programme und `settings.json` verwenden jetzt englische, sprachneutrale Namen (z. B. `Values` statt `Werte`, `Profile` statt `Profil`, Platzhalter `{Date}` statt `{Datum}`). **Alles Vorhandene liest v2 weiter** und stellt die `settings.json` beim ersten Start um – eine **ältere Version** liest die neuen Dateien und die umgestellte `settings.json` aber nicht mehr vollständig.
- Die mitgelieferten Programme liegen jetzt in `Dokumente\ErgoWattControl\Programs` mit englischen Dateinamen (z. B. `S08_FTP200_Threshold_blocks_30min.json`). Der alte Ordner `Programme` wird nicht mehr benutzt und kann gelöscht werden.
- Neue Installationen speichern Aufnahmen in `Recordings` statt `Aufzeichnungen`; bestehende behalten ihren Ordner. Das Protokoll heißt jetzt `ErgoWattControl.log` (auf Englisch).
- Die deutschen Texte stecken im Unterordner `de\` – bei einem Update immer das ganze Setup oder ZIP verwenden, nicht nur die exe kopieren.

### Neu in v1.5.1
- **Stabiler bei Störungen:** Kommt eine Antwort des Cockpits verrutscht an (Reste einer verspäteten Antwort), wird sie verworfen und neu angefragt, statt falsche Werte anzuzeigen. Auch das Nachstellen der Leistung und das Neu-Einstellen nach einem Cockpit-Reset werden bei einem Aussetzer sofort wiederholt.
- **Cockpit-Reset im Protokoll:** Fällt das Cockpit aus dem freien Training, steht das in `Fehler.log`; klappt das Neu-Einstellen dreimal hintereinander nicht, verbindet sich die App neu.
- **Keine stillen Abstürze:** Unerwartete Fehler werden mit allen Details in `Fehler.log` geschrieben und gemeldet – die App läuft weiter. Eine Aufnahme wird auch bei einem Fehler beim Schließen sauber beendet.
- **Einstellungen sicher gespeichert:** `settings.json` wird nie halb geschrieben; eine beschädigte Datei wird als `settings.json.defekt` aufgehoben, die App startet mit Standardwerten.
- **Programmansicht:** Die Pulskurve kostet auch bei langen Programmen nicht mehr Rechenzeit.
- **Unittests:** 46 automatische Tests (u. a. gegen ein simuliertes Cockpit) laufen vor jedem Release.

### Neu in v1.5.0
- **FIT-Dateien nachfahren:** Aktivitäten aus Garmin Connect, Strava, Zwift oder vom Radcomputer – mit Leistungswerten als Leistungsprofil („Aufzeichnung nachfahren“), mit GPS und Höhe ohne Leistung (z. B. MTB-Touren) als Strecke wie eine GPX-Datei. Pausen werden übersprungen, Garmins nummerierte Dateien heißen „Garmin_Datum_Uhrzeit“.
- **Puls im Höhenprofil:** Bei Strecken läuft der Puls in der Übersicht nach Kilometer mit – heute rot, bei einer früheren Fahrt (FIT oder Strecken-Aufzeichnung) der damalige grün.
- **Puls von damals als Zahl:** rechts neben der Pulsanzeige in Grün, an derselben Stelle der früheren Fahrt.
- **Training beenden:** Ein Dialog vor dem Ende mit Ergebnis, Dateiname und **Weiterfahren / Verwerfen / OK** – in allen Modi; auch nach dem Ziel lässt sich die Aufnahme verwerfen. **Enter** beendet jetzt auch Strecken und Programme (nochmal Enter = OK).

### Neu in v1.4.2
- **Stabilere Verbindung zum Cockpit:** Bleibt eine einzelne Antwort aus, fragt die App sofort noch einmal nach (bis zu zweimal), statt die Verbindung abzubauen – kurze Störungen führen nicht mehr zu Sekunden ohne Messwerte, stehendem Graphen und angehaltenem Countdown.
- **Protokoll:** Verbindungsabbrüche, Wiederverbinden und gerettete Anfragen stehen mit Uhrzeit in `Fehler.log`.
- **Hilfe:** neuer Abschnitt zu Aussetzern – u. a. ein Windows-Update mit ausstehendem Neustart (Treiber für Anschlüsse) als mögliche Ursache.

### Neu in v1.4.1
- **Export sicherer:** FIT und CSV werden erst vollständig in eine temporäre Datei geschrieben und dann umbenannt – ein fehlgeschlagener Export hinterlässt keine 0-Byte-Datei mehr und überschreibt keine vorhandene gute Datei.
- **Verständliche Fehler beim Export:** Der Dialog zeigt Meldung und Fehlerart (auch Fehler der FIT-Bibliothek), der vollständige Fehler steht in `Fehler.log`; die übrigen markierten Aufzeichnungen werden trotzdem exportiert.

### Neu in v1.4.0
- **☰ → Info …:** Version der App (mit Commit), Links zur Projektseite, zu neuen Versionen und zur Online-Hilfe sowie die Ordner für Einstellungen und Daten
- README und Hilfe mit Screenshots aller vier Modi (freies Training, Strecke, Programm, Aufzeichnung nachfahren)

### Neu in v1.3.0
- **Eigene Trainingsprogramme:** JSON-Dateien im Ordner `CustomPrograms` (Dokumente\ErgoWattControl bzw. neben der exe) erscheinen mit ★ oben in der Programmliste. Die App legt dort ein JSON-Schema (für Prüfung und Vorschläge im Editor), eine Vorlage und `CLAUDE.md`/`AGENTS.md` ab, damit KI-Assistenten wie Claude Code oder Codex direkt Programme schreiben können. Fehlerhafte Dateien werden mit Grund angezeigt.
- **Neues Programm S08 Schwellenblöcke 30 min** (FTP 150/200/250): 3 × 5 min bei 92 % FTP – Kraftausdauer in 30 min, knieschonend mit hoher Trittfrequenz
- **Fahransicht:** über dem Puls steht, was gerade gefahren wird – Strecke, Programm oder Aufzeichnung

### Neu in v1.2.0
- **Upload zu Garmin Connect und Strava:** im Exportdialog „Danach hochladen bei …“ – die Plattform öffnet sich in einem eingebauten Browserfenster und die FIT-Datei wird auf der Import-/Upload-Seite eingesetzt. Du meldest dich selbst an, die App speichert kein Kennwort. Plattformen, Anmelden/Abmelden und Prüfung der Anmeldung in den Einstellungen.
- **Exportdialog:** Vorschau der Aufzeichnung (Leistung, Puls, Dauer, Strecke), Löschen in den Papierkorb (auch mit Entf), Tooltips für lange Namen
- **Programmansicht:** Puls in der Gesamtübersicht (heute rot, bei Wiederholungen der damalige grün), größere Graphen, kompaktere Fußzeile
- **Monitore:** Taste 0 bzw. der Knopf „Hauptbildschirm/Fahransicht“ schaltet zwischen Fahrer-Monitor und Hauptmonitor um; die Demo zeigt die Fahransicht in der Größe des 1280×1024-Fahrer-Monitors
- **Gerät in den Einstellungen wählbar** (Daum RS232 oder Demo) – intern eine Geräteschicht, an die sich weitere Ergometer anbinden lassen

### Installation mit Setup (empfohlen)
`ErgoWattControl-Setup-….exe` starten. Das Setup installiert die **.NET 10 Desktop Runtime**, falls sie fehlt (dafür fragt Windows nach Administratorrechten), und dann die App für den angemeldeten Benutzer nach `%LocalAppData%\Programs\ErgoWattControl` – mit Verknüpfung im Startmenü und, wenn angehakt, auf dem Desktop. Am Ende kannst du die App gleich starten, auf Wunsch im Demo-Modus. Ein neueres Setup aktualisiert die App; Einstellungen (`%AppData%\ErgoWattControl`) und Aufzeichnungen (`Dokumente\ErgoWattControl`) bleiben erhalten, auch beim Deinstallieren (Windows-Einstellungen → Apps).

### Oder als ZIP
1. `ErgoWattControl-…-win-x64.zip` in einen eigenen Ordner entpacken, z. B. `C:\ergometer\App`.
2. `ErgoWattControl.exe` starten. Einstellungen landen in `%AppData%\ErgoWattControl`, Aufzeichnungen, Strecken und Programme in `Dokumente\ErgoWattControl`.

**Portabel / Update einer älteren Version:** Liegt eine `settings.json` neben der exe, bleibt alles im Programmordner – ältere Installationen laufen also unverändert weiter.

Braucht die [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) – fehlt sie, bietet Windows beim Start den Download an. Update: neue Dateien über die alten kopieren.

### Erste Schritte
Unter **☰ → Einstellungen** den COM-Port des Cockpits wählen. Ohne Ergometer ausprobieren: `ErgoWattControl.exe -demo` (simulierter Fahrer).

### Voraussetzungen
- Windows 10/11 (64 Bit)
- Microsoft Edge WebView2 Runtime für das Hilfefenster und den Upload zu Garmin Connect/Strava – unter Windows 10/11 meist vorinstalliert; ohne sie öffnen sich Hilfe bzw. Plattform im Browser

### Funktionen
- Freies Training mit Wattvorgabe, Fahransicht für einen Monitor am Rad
- Strecke fahren (GPX) mit Steigungssimulation und 28 Gängen
- Aufzeichnungen nachfahren und Trainingsprogramme (Handbuch-Programme P01–P44, Programme nach aktueller Trainingswissenschaft S00–S08)
- Aufzeichnung als JSON, Export als FIT (Strava, Garmin Connect, intervals.icu, TrainingPeaks) und CSV (Excel), Upload zu Garmin Connect und Strava aus der App
