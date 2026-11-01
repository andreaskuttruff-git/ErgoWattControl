Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232).

📖 **[Hilfe / Bedienungsanleitung](https://andreaskuttruff-git.github.io/ErgoWattControl/Hilfe.html)** – in der App mit F1.

### Installation
1. Das ZIP in einen eigenen Ordner entpacken, z. B. `C:\ergometer\App`.
2. `ErgoWattControl.exe` starten. Beim ersten Start legt die App `settings.json`, `Aufzeichnungen\` und `Programme\` neben der exe an.
3. Unter **☰ → Einstellungen** den COM-Port des Cockpits wählen.

Ohne Ergometer ausprobieren: `ErgoWattControl.exe -demo` (simulierter Fahrer).

**Update:** neue Dateien über die alten kopieren – `settings.json` und `Aufzeichnungen\` bleiben erhalten.

### Voraussetzungen
- Windows 10/11 (64 Bit)
- [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) – fehlt sie, bietet Windows beim Start den Download an
- Microsoft Edge WebView2 Runtime für das Hilfefenster – unter Windows 10/11 meist vorinstalliert; ohne sie öffnet sich die Hilfe im Browser

### Funktionen
- Freies Training mit Wattvorgabe, Fahransicht für einen Monitor am Rad
- Strecke fahren (GPX) mit Steigungssimulation und 28 Gängen
- Training wiederholen und Trainingsprogramme (Handbuch-Programme P01–P44, Programme nach aktueller Trainingswissenschaft S00–S07)
- Aufzeichnung als JSON, Export als FIT (Strava, Garmin Connect, intervals.icu, TrainingPeaks) und CSV (Excel)
