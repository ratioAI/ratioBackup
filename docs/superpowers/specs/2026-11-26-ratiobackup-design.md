# RatioBackup – Design

Stand: 26.11.2026 · Status: Entwurf, wartet auf Freigabe

## Wozu
Das Handy (Xiaomi 18 Pro) soll seine wichtigen Dateien nicht mehr bei Google sichern (dort sind nur 10 GB frei, allein die Fotos haben 30 GB), sondern automatisch auf Alexanders Windows-PC. Der PC soll nur einen aktuellen Stand halten und keine alten Backups ansammeln, damit der belegte Platz in einem Rahmen von 20–100 GB bleibt.

## Ziele
- Gesichert werden **Fotos, Videos, Downloads und WhatsApp** (der Medienordner plus die lokale Chat-Sicherung, die WhatsApp jede Nacht selbst anlegt).
- Alles andere bleibt bei der Google-Sicherung.
- Die Sicherung arbeitet als **Spiegel**: Übertragen wird nur, was neu oder geändert ist. Auf dem PC liegt immer genau ein aktueller Stand.
- Auf dem Handy gelöschte Dateien kommen auf dem PC in einen **Papierkorb** und werden dort nach **30 Tagen** endgültig gelöscht.
- Die Sicherung läuft **täglich ab 20 Uhr**, nur im Heim-WLAN. Ist der PC nicht erreichbar, folgt der nächste Versuch **am nächsten Tag**.
- Alles läuft automatisch, man muss nichts anklicken.

## Nicht-Ziele
- Keine Sicherung von App-internen Daten (Spielstände, Einstellungen); das geht ohne Root nicht und bleibt bei Google.
- Keine Sicherung von unterwegs oder über das Internet.
- Keine Versionsgeschichte (keine „Stände von letzter Woche"), nur Spiegel plus Papierkorb.
- Kein Wiederherstellen per Knopfdruck aufs Handy (die Dateien liegen als normale Ordner auf dem PC und lassen sich bei Bedarf einfach zurückkopieren).

## Anti-Ziele (wäre ein Fehlschlag, auch wenn es „funktioniert")
- Fotos gehen verloren, weil ein versehentliches Löschen am Handy sofort auf den PC durchschlägt.
- Die App wird von HyperOS still beendet und sichert wochenlang nicht, ohne dass es jemand merkt.
- Der PC-Speicher läuft unbemerkt voll.
- Werbung, Abo oder Cloud-Zwang.

## Rahmenbedingungen
- Das Handy ist ein Xiaomi 18 Pro mit HyperOS (Android). Die App wird als APK selbst installiert, nicht über den Play Store.
- Der PC läuft mit Windows und ist meist ab 20 Uhr an.
- Alexander programmiert selbst in C#/.NET.

## Lösungswege (betrachtet)
| Weg | Vorteil | Nachteil |
|---|---|---|
| **A) Eigene Handy-App + eigenes PC-Programm** ✅ gewählt | volle Kontrolle (20 Uhr, Papierkorb, Speicherlimit, Status), PC-Teil in C# | zwei Programme zu pflegen |
| B) Eigene Handy-App + Windows-Ordnerfreigabe | kein PC-Programm nötig | Einrichtung der Freigabe fummelig; Papierkorb und Limit übers Netzwerk weniger robust |
| C) Syncthing-Fork | fertig, kostenlos, ohne Werbung | Wartung wechselt oft den Besitzer, keine feste Uhrzeit und kein Speicherlimit |

Alexander hat sich für **A** entschieden, weil er eine eigene App möchte.

## Wie es funktioniert

### Handy-App „RatioBackup" (Kotlin, Android)
- **Berechtigung:** „Zugriff auf alle Dateien", damit die App DCIM, Pictures, Movies, Download und `Android/media/com.whatsapp/WhatsApp/` (Media und Databases) lesen kann.
- **Planung:** täglicher Job über WorkManager ab 20:00, nur wenn das Handy im WLAN ist.
- **Ablauf:** PC im Netzwerk finden → Liste der Dateien vergleichen (Pfad, Größe, Änderungsdatum) → nur neue und geänderte Dateien hochladen → dem PC melden, welche Dateien gelöscht wurden.
- **Wiederaufnahme:** Bricht die Übertragung ab (z. B. WLAN weg), macht der nächste Lauf an der Stelle weiter.
- **Oberfläche:** ein Bildschirm mit letzter Sicherung, nächster Sicherung, belegtem Platz auf dem PC und dem Knopf „Jetzt sichern".
- **HyperOS:** Die Einrichtung führt durch „Autostart erlauben" und „Akku: keine Einschränkungen", sonst beendet HyperOS die App.

### PC-Programm „RatioBackup Empfänger" (C#/.NET, Windows)
- Läuft als Symbol in der Taskleiste und startet mit Windows.
- Ablage als normale Ordner, gleiche Struktur wie auf dem Handy, z. B. `…\RatioBackup\Xiaomi 18 Pro\DCIM\Camera\…`
- Papierkorb unter `…\RatioBackup\_Papierkorb\`; dort wird nach 30 Tagen automatisch geleert.
- Ein Rechtsklick aufs Symbol zeigt Status, letzte Sicherung und belegten Platz und öffnet den Ordner.

### Verbindung
- Das Handy findet den PC automatisch im Heimnetz.
- Die Übertragung ist verschlüsselt, und nur das eigene Handy darf senden.

## Meine Entscheidungen (bitte prüfen)
1. **Name:** RatioBackup, passend zu deinen anderen Ratio-Programmen.
2. **Zeitfenster:** Ab 20:00 versucht die App es bis 20:30 alle 5 Minuten (der PC braucht etwas zum Hochfahren). Klappt es nicht, folgt der nächste Versuch am nächsten Tag um 20 Uhr.
3. **Handy nicht zu Hause um 20 Uhr:** wird genauso behandelt, also nächster Tag.
4. **Koppeln:** Der PC zeigt beim ersten Start einen 6-stelligen Code, den du einmal am Handy eingibst.
5. **Speicherort:** Den Ordner wählst du beim ersten Start des PC-Programms (gern ein anderes Laufwerk als C:).
6. **Speicherlimit:** standardmäßig 100 GB, änderbar. Bei 90 % kommt eine Warnung. Ist es voll, wird zuerst der Papierkorb geleert; reicht das nicht, pausiert die Sicherung mit Hinweis, gelöscht wird aber nichts Aktuelles.
7. **Warnung:** Gab es 3 Tage lang keine erfolgreiche Sicherung, zeigt das Handy eine Benachrichtigung.
8. **Erste Sicherung:** Beim ersten Mal werden etwa 30 GB oder mehr übertragen; das dauert im WLAN ungefähr 30–60 Minuten. Wird sie an einem Abend nicht fertig, macht die App am nächsten Abend dort weiter.
9. **Verteilung:** Die APK kommt wie bei Jarvis über GitHub (Vorschlag: Repo `ratioAI/RatioBackup`), das PC-Programm als einzelne .exe.

## Dem Entwickler überlassen
- Konkretes Übertragungsprotokoll (z. B. HTTPS mit selbstsigniertem, beim Koppeln festgehaltenem Zertifikat) und Gerätesuche (mDNS).
- Datenbank für den Dateiabgleich auf beiden Seiten.
- Aussehen der Oberflächen im Detail.

## Danach (nicht Teil der App)
1. Google-Fotos-Sicherung ausschalten.
2. Wenn die erste PC-Sicherung vollständig ist: alte Fotos auf photos.google.com löschen (im Browser, nicht in der App), um die 10 GB bei Google freizubekommen.
