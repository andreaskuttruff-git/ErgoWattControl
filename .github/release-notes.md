Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232).

📖 **[Hilfe / Bedienungsanleitung](https://andreaskuttruff-git.github.io/ErgoWattControl/Help.de.html)** · [Help (English)](https://andreaskuttruff-git.github.io/ErgoWattControl/Help.en.html) – in der App mit F1.

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
