# RatioBackup 2 – Phase 1 (Sofort-Version) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Live-Fortschritt mit Prozent, Klartext-Fehler, Verlauf und Abbrechen am Handy; Selbst-Update für Handy und PC; neuer Signaturschlüssel und Versionierung.

**Architecture:** Reine Kotlin-Klassen (`ProgressTracker`, `RunHistory`, `UpdateInfo`) tragen die Logik und sind JVM-getestet; `BackupService` veröffentlicht den Zustand als `StateFlow` in `BackupState`, die Compose-Übersicht beobachtet ihn. Updates kommen aus dem Release `test` über `version.json`. Am PC prüft `UpdateChecker` (Core) und tauscht `SelfUpdater` die EXE.

**Tech Stack:** wie Phase 0; zusätzlich Android `PackageInstaller`, .NET `HttpClient`.

**Spec:** `docs/superpowers/specs/2026-11-26-ratiobackup-v2-design.md` (Abschnitte „Versionen und Updates", „Live-Fortschritt", „Verlauf"; Ablauf Schritt 1)

## Global Constraints

- `versionCode = 100 + github.run_number`, `versionName = "0.<run_number>"`; PC-EXE `Version = 0.<run_number>.0`.
- `version.json`: `{"versionCode":123,"versionName":"0.23","notes":"…","apk":"RatioBackup.apk","exe":"RatioBackup-PC.exe"}`.
- Update-URLs: `https://github.com/ratioAI/ratioBackup/releases/download/test/<Datei>`.
- Update-Prüfung beim Öffnen und höchstens alle 6 h; PC-Update nie während einer laufenden Sicherung.
- Prozent = übertragene Bytes ÷ zu übertragende Bytes.
- Verlauf Handy: 30 Läufe.
- Neuer Signaturschlüssel, verschlüsselt im Repo (`android/keystore/release.jks.enc`, openssl AES-256-CBC, PBKDF2), Passwort im Secret `KEYSTORE_PASSWORD`.
- Texte auf Deutsch.

## Review Focus

1. **Update schlägt halb fehl** (Download abgebrochen, PC-Neustart scheitert): Die alte Version muss weiterlaufen. → Test `ApplyUpdate_MissingNewFile_KeepsCurrent` in Task 5.
2. **`version.json` kaputt oder nicht erreichbar** (kein Internet, GitHub down): kein Absturz, nur „keine Info". → Tests `parse_invalid_null` (Task 3) und `Check_HttpError_ReturnsNull` (Task 5).
3. **Fortschritt bei 0 zu übertragenden Bytes** (alles schon gesichert): 100 %, keine Division durch 0, keine Restzeit. → Test `zeroBytes_is100Percent` in Task 2.
4. **Abbrechen mitten im Upload:** Lauf endet als „Abgebrochen", kein `complete`, nächster Lauf setzt fort. → Test `cancelled_returnsCancelled_noComplete` in Task 2.
5. **Uhr springt / Tempo bei sehr kurzen Läufen:** keine negative oder unendliche Restzeit. → Test `eta_neverNegative_orInfinite` in Task 2.

---

### Task 1: Signatur, Versionierung, Release mit `version.json`

**Files:**
- Create: `android/keystore/release.jks.enc`
- Delete: `android/keystore/ratiobackup-test.jks`
- Modify: `android/app/build.gradle.kts` (Signatur aus `RB_KEYSTORE`/`RB_KEYSTORE_PASSWORD`, fällt lokal auf Debug-Schlüssel zurück; `versionCode`/`versionName` aus `RB_VERSION_CODE`/`RB_VERSION_NAME`), `pc/RatioBackup.Tray/RatioBackup.Tray.csproj` (Version aus `-p:Version`), `.github/workflows/build.yml` (Schlüssel entschlüsseln, Versionen setzen, `version.json` erzeugen und hochladen), `.gitignore` (`*.jks`)

- [ ] **Step 1:** Schlüssel erzeugen (`keytool`, Alias `ratiobackup`, RSA 4096, 10000 Tage), mit `openssl enc -aes-256-cbc -pbkdf2 -iter 200000` verschlüsseln; Klartext-Schlüssel nie committen.
- [ ] **Step 2:** Workflow: `android`-Job entschlüsselt mit `${{ secrets.KEYSTORE_PASSWORD }}` nach `$RUNNER_TEMP/release.jks`; fehlt das Secret, schlägt der Job mit „KEYSTORE_PASSWORD fehlt" fehl. `release`-Job erzeugt `version.json` und lädt sie mit hoch.
- [ ] **Step 3:** Push → `tools/ci-wait`: alle Jobs grün (erst nachdem Alexander das Secret gesetzt hat).
- [ ] **Step 4:** Commit `chore: neuer Signaturschlüssel, Versionierung, version.json`.

### Task 2: Fortschritt und Abbrechen im Sicherungslauf (Handy)

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/backup/ProgressTracker.kt`
- Modify: `backup/BackupRunner.kt`, `net/Models.kt` (`BackupApiPort.upload` bekommt `onBytes: (Long) -> Unit`), `net/BackupApi.kt` (`FileTailBody` meldet geschriebene Bytes)
- Test: `android/app/src/test/java/de/ratio/backup/ProgressTrackerTest.kt`, `BackupRunnerTest.kt` (anpassen + neue Tests)

**Interfaces:**
- Produces:
  - `data class Progress(val doneFiles: Int, val totalFiles: Int, val doneBytes: Long, val totalBytes: Long, val bytesPerSec: Long, val etaSec: Long?, val currentFile: String?) { val percent: Int }`
  - `class ProgressTracker(totalFiles: Int, totalBytes: Long, nowMs: () -> Long) { fun addBytes(n: Long); fun fileStarted(path: String); fun fileDone(); fun snapshot(): Progress }` — Tempo als gleitender Durchschnitt über die letzten 10 s.
  - `BackupRunner.run(onProgress: (Progress) -> Unit): RunOutcome`; neues `RunOutcome.Cancelled`; Abbruch über Coroutine-Cancel (`run` fängt `CancellationException`, gibt `Cancelled` zurück, ohne `complete`).
  - `BackupApiPort.upload(file: File, entry: FileEntry, offset: Long, onBytes: (Long) -> Unit): UploadResult`
- [ ] **Step 1: Failing tests:** `percent_fromBytes` (25 von 100 Byte → 25); `zeroBytes_is100Percent`; `speed_isBytesPerSecond` (1 MB in 2 s → 500 000 B/s ± 1 %); `eta_fromRemainingAndSpeed`; `eta_neverNegative_orInfinite` (Tempo 0 → `etaSec == null`); `runner_reportsBytesAndFiles` (letzter Fortschritt: doneFiles == totalFiles, doneBytes == totalBytes); `cancelled_returnsCancelled_noComplete`.
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren.
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(android): Fortschritt und Abbrechen`.

### Task 3: Verlauf und Update-Info (Handy, reine Logik)

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/backup/RunHistory.kt`, `update/UpdateInfo.kt`
- Test: `android/app/src/test/java/de/ratio/backup/RunHistoryTest.kt`, `UpdateInfoTest.kt`

**Interfaces:**
- Produces:
  - `@Serializable data class RunRecord(val startMs: Long, val endMs: Long, val uploadedFiles: Int, val skippedFiles: Int, val bytes: Long, val result: String /* ok|pc_not_found|quota|no_access|cancelled|failed|not_paired */, val message: String)`
  - `object RunHistory { const val MAX = 30; fun add(json: String?, r: RunRecord): String; fun parse(json: String?): List<RunRecord> /* neueste zuerst */ }`
  - `@Serializable data class UpdateInfo(val versionCode: Int, val versionName: String, val notes: String = "", val apk: String = "RatioBackup.apk", val exe: String = "RatioBackup-PC.exe") { companion object { fun parse(json: String): UpdateInfo?; const val BASE = "https://github.com/ratioAI/ratioBackup/releases/download/test/" } fun isNewerThan(code: Int): Boolean; val apkUrl: String }`
- [ ] **Step 1: Failing tests:** `add_keepsNewestFirst`; `add_capsAt30`; `parse_garbage_empty`; `parse_valid`; `parse_invalid_null`; `isNewer_comparesCode`; `apkUrl_built`.
- [ ] **Step 2–4:** Push → FAIL, implementieren, Push → PASS.
- [ ] **Step 5:** Commit `feat(android): Verlauf und Update-Info`.

### Task 4: Dienst, Übersicht, Update-Installation (Handy)

**Files:**
- Create: `backup/BackupState.kt` (`object BackupState { val flow: StateFlow<UiState> }` mit `sealed interface UiState { Idle; Searching; Comparing; Uploading(Progress); Finished(RunRecord) }`), `update/Updater.kt` (`suspend fun check(context): UpdateInfo?` mit 6-h-Drosselung in Prefs; `suspend fun downloadAndInstall(context, info, onProgress: (Int) -> Unit)` über `PackageInstaller`-Sitzung), `update/InstallReceiver.kt`
- Modify: `backup/BackupService.kt` (Zustand veröffentlichen, `ACTION_CANCEL`, Benachrichtigung mit Prozent + „Abbrechen", Verlauf schreiben), `backup/Prefs.kt` (`historyJson`, `lastUpdateCheck`), `ui/MainScreen.kt` (Fortschrittsring mit %, Dateien, MB, Tempo, Restzeit, aktuelle Datei, Abbrechen; letzter Fehler in Klartext; Update-Karte; einfacher Verlauf), `AndroidManifest.xml` (`REQUEST_INSTALL_PACKAGES`, Receiver)
- [ ] **Step 1:** Implementieren; Klartext-Meldungen je `result` (z. B. `pc_not_found` → „PC nicht gefunden. Ist RatioBackup am PC gestartet und das Handy im selben WLAN?").
- [ ] **Step 2:** Push → android grün (APK baut, alle Tests grün).
- [ ] **Step 3:** Commit `feat(android): Live-Fortschritt, Verlauf, Selbst-Update`.

### Task 5: Selbst-Update am PC

**Files:**
- Create: `pc/RatioBackup.Core/UpdateChecker.cs`, `pc/RatioBackup.Core/SelfUpdater.cs`
- Modify: `pc/RatioBackup.Tray/TrayApp.cs` (Prüfung beim Start und alle 6 h, Sprechblase, Menü „Nach Updates suchen"/„Update installieren", nicht während `run` aktiv)
- Test: `pc/RatioBackup.Core.Tests/UpdateTests.cs`

**Interfaces:**
- Produces:
  - `record UpdateInfo(int VersionCode, string VersionName, string Notes, string Apk, string Exe)`
  - `class UpdateChecker(HttpClient http, string baseUrl = "https://github.com/ratioAI/ratioBackup/releases/download/test/") { Task<UpdateInfo?> CheckAsync(Version current); Task DownloadExeAsync(UpdateInfo info, string targetPath, IProgress<double>? p = null) }` — neuer als `current`, wenn `Version.Parse(VersionName + ".0") > current`.
  - `static class SelfUpdater { static void ApplyUpdate(string currentExe, string downloadedExe); static void CleanupOld(string currentExe); }` — `current` → `current.old`, `downloaded` → `current`; fehlt `downloaded`, bleibt alles unverändert.
- [ ] **Step 1: Failing tests:** `Check_NewerVersion_ReturnsInfo` (Fake-Handler); `Check_SameVersion_Null`; `Check_HttpError_ReturnsNull`; `Check_Garbage_ReturnsNull`; `ApplyUpdate_SwapsFiles`; `ApplyUpdate_MissingNewFile_KeepsCurrent`; `CleanupOld_DeletesOld`.
- [ ] **Step 2–4:** Push → FAIL, implementieren, Push → PASS.
- [ ] **Step 5:** Commit `feat(pc): Selbst-Update`.

### Task 6: Abschluss Phase 1

- [ ] **Step 1:** Review (unabhängiger Prüfer) des Phase-1-Diffs, Korrekturen mit Tests.
- [ ] **Step 2:** Merge nach `main` → Release `test` mit `RatioBackup.apk`, `RatioBackup-PC.exe`, `version.json`.
- [ ] **Step 3:** Alexander: alte App deinstallieren, neue installieren, neu koppeln, „Jetzt sichern" → Fortschritt sichtbar.
