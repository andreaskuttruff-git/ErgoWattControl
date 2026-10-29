# ErgoWattControl

Steuerung für das Daum **ergo_bike 8008 TRS** über die serielle Schnittstelle (RS232, 9600 8N1) – als Ersatz für das Cockpit-Bedienteil und die Originalsoftware ergo_win 2003.

## Funktionen

- **Freies Training:** Wattvorgabe in 5-W-Schritten (Tasten ↑↓ + −, Mausrad, Bild ↑↓ ±25 W), Live-Daten von Leistung, RPM, Puls, Geschwindigkeit, Zeit
- **Fahransicht** für einen kleinen Monitor am Rad (1280×1024): Verlaufsgraphen für Leistung/RPM und Puls, erlaubter Drehzahlbereich der Wirbelstrombremse als Band, Pfeile für schneller/langsamer treten
- **Aufzeichnung** als CSV (Semikolon, Dezimalkomma – öffnet direkt im deutschen Excel)
- **Strecke fahren:** GPX-Strecke nachfahren; aus Steigung, Gewicht und virtueller Geschwindigkeit (Trittfrequenz × Gang) wird die Bremsleistung berechnet. Übersetzung Rennrad oder MTB, Steigungsfaktor einstellbar
- Automatisches Verbinden und Neuverbinden nach einem Cockpit-Reset
- **Demo-Modus** ohne Ergometer: `ErgoWattControl.exe --demo [strecke.gpx]`

## Aufbau des Quellcodes

| Ordner | Inhalt |
|---|---|
| `Cockpit/` | RS232-Protokoll (`DaumCockpit`), Verbindung/Neuverbinden (`ErgoController`), Drehzahlbereich der Bremse, Demo-Fahrer |
| `RideView/` | Fahransicht (`RideForm`), Verlaufsgraph, Mausrad, Zusammenfassung, Monitorauswahl |
| `Recording/` | CSV-Aufzeichnung |
| `Route/` | GPX-Strecke, Simulation (Gänge, Steigung, Leistung), Höhenprofil, Streckenübersicht |
| `Replay/` | gespeichertes Training wiederholen: Einlesen, Ablauf, Leistungsprofil, Übersicht |
| `Settings/` | `settings.json` und Einstellungsdialog |

## Bauen

.NET 10, Windows Forms:

```
dotnet publish -c Release -o publish
```

## Konfiguration

`settings.json` neben der .exe (wird beim ersten Start angelegt): COM-Port, Ablageordner, Monitor der Fahransicht, Gewichte, Übersetzung, Steigungsfaktor sowie die Dateinamen-Muster der Aufzeichnungen (`{Datum}`, `{Uhrzeit}`, `{Strecke}`).
