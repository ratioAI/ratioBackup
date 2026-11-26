# RatioBackup 2 – Design (Oberfläche, Fortschritt, Updates, PC-Fenster)

Stand: 26.11.2026 · Status: Entwurf, wartet auf Freigabe · baut auf `2026-11-26-ratiobackup-design.md` auf

## Wozu
Beim ersten echten Test hat Alexander auf „Jetzt sichern" getippt und „nichts passiert" gesehen – tatsächlich liefen im Hintergrund schon 18 GB, nur ohne sichtbare Rückmeldung. Er will:
- Live-Rückmeldung mit Prozentanzeige,
- eine moderne App mit Dunkelmodus,
- ein richtiges Programmfenster am PC,
- Updates direkt aus der App, ohne Neuinstallation von Hand,
- dass Claude als Software- und UI-Experte sinnvolle Funktionen ergänzt und nach einem festen Ablauf arbeitet.

## Ziele
1. **Sichtbarkeit:** Jeder Lauf ist live zu sehen (Handy und PC): Prozent, Dateien x/y, übertragene MB, Tempo, Restzeit, aktuelle Datei. Fehler stehen in Klartext mit Handlungsanweisung.
2. **Modernes Handy-Design:** Material 3, Farben nach Hintergrundbild (Android 12+), hell und dunkel nach Systemeinstellung, drei Bereiche: **Übersicht · Verlauf · Einstellungen**.
3. **PC-Fenster:** Windows-11-Optik (Fluent, Mica), hell und dunkel nach Windows, Bereiche **Übersicht · Papierkorb · Verlauf · Einstellungen**; das Taskleistensymbol bleibt, Schließen des Fensters minimiert in die Taskleiste.
4. **Selbst-Update:** Handy-App und PC-Programm prüfen auf neue Versionen, zeigen sie an und installieren mit einem Tipp/Klick.
5. **Zusatzfunktionen** (Claudes Vorschlag, von Alexander mit „passt" bestätigt):
   - Handy: Sicherung abbrechen; Uhrzeit einstellbar (Standard 20:00); Ordner einzeln an/aus (Fotos & Videos, Screenshots/Bilder, Downloads, WhatsApp) mit Größe; Verlauf der letzten 30 Läufe.
   - PC: Papierkorb ansehen und Dateien **wiederherstellen** (beim nächsten Lauf nicht erneut gelöscht, sondern am PC zurück in den Spiegel) oder endgültig löschen; zurückgehaltene Löschungen im Fenster prüfen; Speicher-Ring (Spiegel / Papierkorb / frei bis Limit); Verlauf; Einstellungen (Ordner, Limit, Autostart, neu koppeln **mit Prüfcode**).

## Nicht-Ziele
- Keine Fotogalerie am PC (der Ordner wird im Explorer geöffnet).
- Kein Wiederherstellen aufs Handy per Knopf.
- Keine Installation ohne jeden Tipp (Android erlaubt das nur über Stores).

## Anti-Ziele
- Ein Update, das die Kopplung oder die Sicherung zerstört.
- Hübsche Oberfläche, die beim Fortschritt lügt (z. B. 100 % ohne `complete`).
- Oberfläche, die während großer Uploads hängt.

## Rahmenbedingungen
- Repo `ratioAI/ratioBackup` wird **öffentlich** (Alexanders Entscheidung), damit Updates ohne Anmeldung ladbar sind.
- Der bisherige Test-Signaturschlüssel liegt im Git-Verlauf und gilt damit als öffentlich → **neuer Schlüssel**, verschlüsselt im Repo, Passwort als GitHub-Secret `KEYSTORE_PASSWORD`. Folge: Die jetzige App muss **einmal deinstalliert** werden (Kopplung danach neu). Die Dateien auf dem PC bleiben; der nächste Lauf überträgt nur, was fehlt.
- Die laufende erste Sicherung soll vorher fertig werden.
- Weiterhin: Kotlin/Compose, C#/.NET 8, Build und Tests nur in GitHub Actions.

## Lösungswege
| Bereich | Gewählt | Verworfen und warum |
|---|---|---|
| PC-Oberfläche | **WPF + WPF-UI** (Fluent/Mica, Dunkelmodus, eine EXE) | WinForms (sieht nicht modern aus); WinUI 3 (Paketierung als einzelne EXE umständlich); Web-Oberfläche im Browser (wirkt nicht wie ein Programm) |
| Update-Quelle | **GitHub-Release `test`** mit `version.json` | Update über den PC (PC müsste sich selbst erst aktualisieren); privates Repo mit Token (Alexander wählte öffentlich) |
| Handy-Installation | **PackageInstaller-Sitzung** (ein Tipp „Installieren") | Browser-Download (mehr Schritte) |
| PC-Update | **EXE umbenennen und ersetzen**, dann Neustart | Installer/MSIX (überdimensioniert) |
| Live-Fortschritt PC | **PC rechnet selbst** aus Abgleich + empfangenen Bytes | Handy meldet Fortschritt zusätzlich (doppelte Logik) |

## Wie es funktioniert

### Versionen und Updates
- CI setzt `versionCode = 100 + github.run_number`, `versionName = "0.<run_number>"` (Handy) und dieselbe Version für die PC-EXE.
- Release `test` enthält zusätzlich `version.json`: `{"versionCode":123,"versionName":"0.23","notes":"…","apk":"RatioBackup.apk","exe":"RatioBackup-PC.exe"}`.
- Feste URLs: `https://github.com/ratioAI/ratioBackup/releases/download/test/<Datei>`.
- Handy: Prüfung beim Öffnen der App und höchstens alle 6 h; Karte „Update verfügbar · 0.23 · Installieren"; Download mit Fortschritt; Installation über PackageInstaller; Berechtigung „Apps installieren" wird beim ersten Mal erklärt und angefragt.
- PC: Prüfung beim Start und alle 6 h; Hinweis im Fenster und als Sprechblase; „Jetzt aktualisieren" lädt die EXE, benennt die laufende in `.old` um, legt die neue an ihren Platz, startet neu; `.old` wird beim nächsten Start gelöscht. Kein Update während eine Sicherung läuft.

### Live-Fortschritt
- **Handy:** Der Dienst veröffentlicht einen Zustand (`Idle | Searching | Comparing | Uploading(done/total Dateien, Bytes, Tempo, Restzeit, Datei) | Finished(Ergebnis)`) als StateFlow; Übersicht und Benachrichtigung zeigen ihn (Benachrichtigung mit Prozent und „Abbrechen").
- **PC:** Beim Abgleich merkt sich der PC Gesamtdateien/-bytes der benötigten Dateien und zählt empfangene Bytes mit; `GET /status` liefert zusätzlich `run` (aktiv, Dateien, Bytes, Start). Das Fenster zeigt denselben Fortschritt.
- Prozent = übertragene Bytes ÷ zu übertragende Bytes (ehrlich auch bei großen Videos).

### Verlauf
- Handy: letzte 30 Läufe (Start, Dauer, Dateien, MB, Ergebnis, Fehlertext), lokal gespeichert.
- PC: letzte 100 Läufe im Index-Zustand.

### Papierkorb am PC
- Liste nach Tag gruppiert (Datei, Größe, gelöscht am, endgültig weg in X Tagen), Suche, „Wiederherstellen" (zurück in den Spiegel und Index, damit der nächste Abgleich die Datei nicht als fehlend behandelt) und „Endgültig löschen".
- Wiederhergestellte Dateien, die am Handy wirklich fehlen, würden beim nächsten Lauf erneut im Papierkorb landen → wiederhergestellte Dateien werden als **am PC behalten** markiert und nie mehr automatisch gelöscht.

### Kopplung mit Prüfcode
- PC und Handy zeigen beim Koppeln denselben 4-stelligen Prüfcode (aus dem Zertifikats-Fingerabdruck). Stimmt er nicht überein, bricht man ab. Behebt die im Review offen gelassene Lücke.

### Handy-Bildschirme (Skizze; genaue Optik folgt als Entwurf zur Freigabe)
```
Übersicht                         Verlauf                 Einstellungen
┌──────────────────────────┐      ┌────────────────────┐  ┌──────────────────────┐
│ RatioBackup     [Update] │      │ Heute 20:00  ✓     │  │ Uhrzeit       20:00 >│
│      ╭──────╮            │      │ 1.204 Dateien·3 GB │  │ Fotos & Videos   ● on │
│      │ 42 % │  Ring      │      │ Gestern 20:05 ✗    │  │ Downloads        ● on │
│      ╰──────╯            │      │ PC nicht gefunden  │  │ WhatsApp         ● on │
│ 512 / 1.204 · 12 MB/s    │      │ …                  │  │ PC: MEIN-PC  neu koppeln│
│ noch ca. 4 Min           │      └────────────────────┘  │ Freigaben prüfen     >│
│ [ Abbrechen ]            │                              │ Version 0.23  Prüfen  │
│ PC-Speicher ▓▓▓░░ 18/100 │                              └──────────────────────┘
│ Nächste: heute 20:00     │
└─[Übersicht][Verlauf][⚙]──┘
```

### PC-Fenster (Skizze)
```
┌ RatioBackup ───────────────────────────────────────────┐
│ ◉ Übersicht  │  Xiaomi 18 Pro · gekoppelt · zuletzt 20:04│
│ 🗑 Papierkorb │  ╭────╮ Sicherung läuft 42 %            │
│ ⏱ Verlauf    │  │ 42%│ 512/1.204 Dateien · 12 MB/s     │
│ ⚙ Einstell.  │  ╰────╯                                  │
│              │  Speicher ◔ 18 GB Spiegel · 1 GB Papierk. │
│              │           von 100 GB                      │
│              │  [Ordner öffnen]  [Update 0.23 verfügbar] │
└──────────────┴───────────────────────────────────────────┘
```

## Ablauf (Workflow)
1. **Sofort-Version:** Live-Fortschritt + Klartext-Fehler + Verlauf am Handy, Selbst-Update (Handy + PC), neuer Signaturschlüssel, Versionierung → Release. Alexander deinstalliert einmal und installiert diese Version.
2. **Entwürfe:** klickbare Entwürfe für Handy (hell/dunkel) und PC → Freigabe durch Alexander.
3. **Handy-Design** nach Entwurf (Material 3, drei Bereiche, Einstellungen inkl. Uhrzeit und Ordner).
4. **PC-Fenster** nach Entwurf (WPF-UI), Live-Fortschritt, Papierkorb mit Wiederherstellen, Verlauf, Einstellungen, Prüfcode.
5. **Review** durch unabhängigen Prüfer, Korrekturen, Release.
6. **Test mit Alexander** über die Update-Funktion.

## Dem Entwickler überlassen
- Konkrete Farben, Abstände, Animationen (innerhalb der freigegebenen Entwürfe).
- Interne Aufteilung der Klassen, Speicherformat des Verlaufs.

## Meine Entscheidungen (bitte prüfen)
1. Update-Prüfung höchstens alle 6 Stunden und beim Öffnen.
2. PC-Update nie während einer laufenden Sicherung.
3. Wiederhergestellte Papierkorb-Dateien bleiben am PC dauerhaft (werden nicht wieder automatisch gelöscht).
4. Verlauf: 30 Läufe am Handy, 100 am PC.
5. Neuer Signaturschlüssel → einmal deinstallieren nötig.
