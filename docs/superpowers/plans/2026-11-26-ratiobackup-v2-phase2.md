# RatioBackup 2 – Phase 2 (neues Design Handy + PC-Fenster) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Handy-App und PC-Programm sehen aus wie die freigegebenen Entwürfe (Canvas „RatioBackup 2 – Entwürfe Handy & PC") und bekommen Uhrzeit, Ordnerauswahl, Prüfcode, Live-Fortschritt am PC, Papierkorb mit Wiederherstellen und Verlauf am PC.

**Architecture:** Logik bleibt in reinen, getesteten Klassen (Kotlin: `Schedule`, `Scanner`, `Categories`, `VerifyCode`; C#: `BackupStore` mit Lauf-Fortschritt, Verlauf, Papierkorb-Einträgen). Die Oberflächen (Compose, WPF) sind dünne Schichten darüber.

**Tech Stack:** Compose Material 3 mit eigener Farbpalette aus den Entwürfen; WPF (.NET 8) + `WPF-UI` 3.x (Fluent, Mica, hell/dunkel nach Windows), WinForms-NotifyIcon für das Taskleistensymbol.

**Spec:** `docs/superpowers/specs/2026-11-26-ratiobackup-v2-design.md`; Optik: die sechs Artboards im Canvas.

## Global Constraints
- Farben (Entwürfe): hell Hintergrund `#f6f7fb`, Karten `#ffffff`, Text `#12151c`, gedämpft `#5b6272`, Akzent `#1d5bd8`; dunkel Hintergrund `#0e1117`, Karten `#161a22`, Text `#e8ebf2`, gedämpft `#9aa3b5`, Akzent `#8db4ff`; Erfolg `#1f9d57`/`#3ccf7f`, Warnung `#e8a23a`/`#f0b45a`. Hell/dunkel folgt dem System.
- Uhrzeit Standard 20:00, Wiederholung alle 5 Min bis Startzeit + 30 Min.
- Kategorien: Kamera = `DCIM`, `Movies`; Bilder & Screenshots = `Pictures`; Downloads = `Download`; WhatsApp = beide WhatsApp-Wurzeln. Abgewählte Kategorien werden nicht gemeldet → der PC löscht dort nie etwas.
- Prüfcode = erste 4 Zeichen des Zertifikat-Fingerabdrucks, Großbuchstaben, auf Handy und PC gleich.
- Verlauf PC: 100 Läufe. Ein Lauf ohne Aktivität seit 2 Min gilt als beendet („unterbrochen").
- Wiederhergestellte Papierkorb-Dateien bleiben dauerhaft am PC (nie wieder automatisch gelöscht).

## Review Focus
1. Uhrzeit geändert, während heute schon gesichert wurde → nächster Lauf morgen zur neuen Zeit (Test `customTime_successToday_tomorrowAtNewTime`).
2. Kategorie abgewählt → deren Dateien auf dem PC bleiben unangetastet (Test `scan_onlySelectedRoots`, PC-Test `Deletion_OnlyWithinListedRoots` existiert).
3. Wiederherstellen einer Datei, deren Name im Spiegel inzwischen wieder belegt ist → kein Überschreiben (Test `Restore_TargetExists_KeepsBoth`).
4. Lauf bricht ab (Handy weg) → PC-Fortschritt hängt nicht ewig auf „läuft" (Test `Run_TimesOutAfterInactivity`).
5. WPF-Fenster schließen → Programm läuft im Hintergrund weiter; „Beenden" im Menü beendet wirklich (manuell, Task 7).

---

### Task 1: Zeitplan mit eigener Uhrzeit, Kategorien, Prüfcode (Handy, Logik)
**Files:** `schedule/Schedule.kt`, `scan/Scanner.kt`, `scan/Categories.kt` (neu), `net/VerifyCode.kt` (neu); Tests `ScheduleTest.kt`, `ScannerTest.kt`, `CategoriesTest.kt`
**Interfaces (Produces):** `Schedule.nextAttempt(now, lastSuccessDate, start: LocalTime = START)`; `Scanner(storageRoot, roots: List<String> = PathFilter.ROOTS)`; `enum class Category(val label: String, val roots: List<String>) { CAMERA, PICTURES, DOWNLOADS, WHATSAPP }` + `fun rootsFor(enabled: Set<Category>): List<String>`; `fun verifyCode(fingerprint: String): String`
- [ ] Tests: `customTime_before_sameDay`, `customTime_window_plus5`, `customTime_afterWindow_tomorrow`, `customTime_successToday_tomorrowAtNewTime`, `scan_onlySelectedRoots`, `rootsFor_whatsapp_bothRoots`, `rootsFor_none_empty`, `verifyCode_first4Upper` → rot → Implementierung → grün.

### Task 2: Neues Handy-Design
**Files:** `ui/Theme.kt` (Palette), `ui/MainActivity.kt` (NavigationBar, 3 Bereiche), `ui/OverviewScreen.kt` (ersetzt MainScreen), `ui/HistoryScreen.kt`, `ui/SettingsScreen.kt`, `ui/Components.kt` (Ring, Karten, Speicherbalken), `backup/Prefs.kt` (`startTime`, `categories`), `backup/BackupService.kt` (Kategorien an Scanner), `schedule/AlarmScheduler.kt` (Startzeit), `ui/SetupScreen.kt` (Prüfcode beim Koppeln anzeigen)
- [ ] Umsetzen nach Artboards Main/PhoneDark/PhoneHistory/PhoneSettings; Ordnergrößen im Hintergrund berechnen.
- [ ] CI: APK baut, alle Tests grün.

### Task 3: PC-Core: Lauf-Fortschritt, Verlauf, Papierkorb-Einträge
**Files:** `BackupStore.cs`, `Models.cs`; Test `BackupStoreRunTests.cs`
**Interfaces (Produces):** `record RunInfo(bool Active, int TotalFiles, int DoneFiles, long TotalBytes, long DoneBytes, DateTime StartedUtc, DateTime LastActivityUtc)`; `StatusDto.Run` (RunInfo?); `record RunRecord(DateTime StartUtc, DateTime EndUtc, int Files, long Bytes, string Result)`; `IReadOnlyList<RunRecord> History`; `record TrashEntry(string Id, string PhonePath, string Stored, string Day, long Size, DateTime DeletedUtc)`; `IReadOnlyList<TrashEntry> ListTrash()`; `bool Restore(string id)`; `void DeletePermanently(IEnumerable<string> ids)`; `PairingService.VerifyCode(string fp)`
- [ ] Tests: `Run_StartsOnManifest_CountsBytes`, `Run_CompleteRecordsHistory`, `Run_TimesOutAfterInactivity`, `History_CappedAt100`, `Trash_ListsEntriesWithPhonePath`, `Restore_MovesBackAndKeepsForever`, `Restore_TargetExists_KeepsBoth`, `DeletePermanently_RemovesFileAndEntry`, `Purge_RemovesEntries`, `VerifyCode_first4Upper` → rot → grün.

### Task 4: PC-Fenster (WPF + WPF-UI)
**Files:** `pc/RatioBackup.Tray/*` auf WPF umstellen: `App.xaml(.cs)`, `MainWindow.xaml(.cs)` (FluentWindow, NavigationView: Übersicht, Papierkorb, Verlauf, Einstellungen), `Pages/*.xaml(.cs)`, `TrayIcon.cs` (WinForms NotifyIcon), bestehende Setup/Pairing-Dialoge als WPF; `Program.cs` (Mutex, Neustart nach Update)
- [ ] Übersicht live (Timer 1 s auf `GetStatus()`), Speicher-Ring, Update-Hinweis; Papierkorb mit Suche, Auswahl, Wiederherstellen, Endgültig löschen, Hinweis bei zurückgehaltenen Löschungen; Verlauf; Einstellungen (Ordner, Limit, Autostart, neu koppeln mit Prüfcode, Updates).
- [ ] Fenster-Schließen → in die Taskleiste; Doppelklick aufs Symbol öffnet es.
- [ ] CI: EXE baut.

### Task 5: Review, Release, Test
- [ ] Unabhängiger Review, Korrekturen mit Tests, Merge → Release; Alexander aktualisiert über die Update-Funktion.
