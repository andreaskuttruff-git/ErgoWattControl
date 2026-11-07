Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232).

📖 **[Hilfe / Bedienungsanleitung](https://andreaskuttruff-git.github.io/ErgoWattControl/Hilfe.html)** – in der App mit F1.

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
- Aufzeichnungen nachfahren und Trainingsprogramme (Handbuch-Programme P01–P44, Programme nach aktueller Trainingswissenschaft S00–S07)
- Aufzeichnung als JSON, Export als FIT (Strava, Garmin Connect, intervals.icu, TrainingPeaks) und CSV (Excel), Upload zu Garmin Connect und Strava aus der App
