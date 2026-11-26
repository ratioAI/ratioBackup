# RatioBackup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine Android-App spiegelt Fotos, Videos, Downloads und WhatsApp jeden Abend ab 20 Uhr übers Heim-WLAN auf ein Windows-Taskleistenprogramm, das einen aktuellen Stand plus 30-Tage-Papierkorb innerhalb eines Speicherlimits hält.

**Architecture:** Monorepo `ratioAI/RatioBackup` mit `android/` (Kotlin, Compose, AlarmManager + Foreground-Service, OkHttp) und `pc/` (.NET 8: `RatioBackup.Core` mit Ablage-/Papierkorb-/Kopplungslogik und HTTPS-API über Kestrel, `RatioBackup.Tray` als WinForms-Taskleistenprogramm). Das Handy findet den PC per UDP-Broadcast, koppelt sich einmal mit einem 6-stelligen Code und pinnt danach das selbstsignierte PC-Zertifikat. Gebaut und getestet wird in GitHub Actions (die Arbeitsumgebung hat keinen Zugriff auf Google Maven, Maven Central oder NuGet).

**Tech Stack:** Kotlin 2.2.21, AGP 8.13.2, Gradle 8.14.3, compileSdk/targetSdk 36, minSdk 31, Jetpack Compose (BOM 2025.10.01), OkHttp 4.12, kotlinx.serialization 1.9, JUnit 4; .NET 8, ASP.NET Core Kestrel (Minimal API), xUnit, Microsoft.AspNetCore.TestHost, WinForms.

**Spec:** `docs/superpowers/specs/2026-11-26-ratiobackup-design.md`

## Global Constraints

- Gesicherte Ordner (relativ zu `/storage/emulated/0`): `DCIM`, `Pictures`, `Movies`, `Download`, `Android/media/com.whatsapp/WhatsApp/Media`, `Android/media/com.whatsapp/WhatsApp/Databases`.
- Spiegel: nur neue/geänderte Dateien (Vergleich Pfad + Größe + Änderungszeit in ms) werden übertragen.
- Papierkorb: auf dem Handy gelöschte Dateien → `<Ablage>\_Papierkorb\yyyy-MM-dd\<Pfad>`; endgültig gelöscht nach **30 Tagen**.
- Zeitplan: täglich ab **20:00** Ortszeit, Versuche alle **5 Minuten** bis **20:30**; danach nächster Versuch am nächsten Tag 20:00. Handy nicht im WLAN = gleicher Fall.
- Speicherlimit Standard **100 GB**, änderbar; Warnung ab **90 %**; voll → erst Papierkorb leeren, dann Sicherung pausieren; aktuelle Dateien werden nie gelöscht.
- Warnung am Handy nach **3 Tagen** ohne erfolgreiche Sicherung.
- Kopplung: **6-stelliger** Code am PC, gültig 5 Minuten.
- Ablage: `<Ablage>\<Gerätename>\<Pfad>`, Gerätename Standard `Xiaomi 18 Pro`; Ablageordner wählt der Nutzer beim ersten Start.
- Ports: UDP **47320** (Suche), TCP **47321** (HTTPS-API).
- Android-Paket `de.ratio.backup`, App-Name `RatioBackup`; Release-Dateien `RatioBackup.apk` und `RatioBackup-PC.exe` im GitHub-Release `test`.
- Sämtliche Texte in der Oberfläche auf Deutsch.
- Keine Werbung, kein Konto, keine Cloud.

## Review Focus

1. **Massenlöschung durch fehlenden Ordner:** Verliert die App die Dateiberechtigung oder wird WhatsApp neu installiert, meldet das Handy plötzlich einen leeren Ordner. Erwartung: Der PC verschiebt **nicht** alles in den Papierkorb. Löschungen über 20 % der Dateien einer Wurzel (mindestens 50 Dateien) werden zurückgehalten und am PC zur Bestätigung angezeigt. → Test in Task 3.
2. **Dateinamen, die Windows nicht erlaubt** (`:*?"<>|`, Endpunkt/Leerzeichen am Ende, `CON`, `NUL` …) oder die sich nur in Groß-/Kleinschreibung unterscheiden (`IMG.jpg` vs. `img.JPG`). Erwartung: Beide werden gesichert, keine überschreibt die andere. → Tests in Task 2 und Task 3.
3. **Abbruch mitten in einem großen Video** (WLAN weg, PC geht in Standby). Erwartung: Der nächste Lauf setzt am erreichten Byte fort, statt neu zu beginnen; halbe Dateien tauchen nie als fertige Datei auf. → Tests in Task 3 und Task 9.
4. **Datei ändert sich während des Hochladens** (WhatsApp schreibt die Chat-Sicherung gerade neu). Erwartung: Stimmen Größe oder Prüfsumme am Ende nicht, wird der Teil verworfen und beim nächsten Lauf neu übertragen. → Test in Task 3.
5. **Sommerzeitwechsel und Mitternacht** (28.03.2027, 31.10.2027; Erfolg um 20:29 an einem Tag, App-Start um 23:59). Erwartung: Nächster Versuch ist immer der richtige Tag um 20:00 Ortszeit, nie doppelt, nie übersprungen. → Tests in Task 8.

---

## File Structure

```
.github/workflows/build.yml           CI: Android-Tests+APK, PC-Tests+EXE, Release "test", Testergebnisse → Branch ci-results
docs/PROTOCOL.md                      HTTP/UDP-Vertrag zwischen Handy und PC (verbindlich für beide Seiten)
pc/RatioBackup.sln
pc/RatioBackup.Core/                  net8.0, plattformunabhängig, voll getestet
  PathMapper.cs                       Handy-Pfad ↔ Windows-Pfad, Escaping, Traversal-Schutz
  BackupStore.cs                      Index, Abgleich, Uploads mit Wiederaufnahme, Papierkorb, Limit, Schutz vor Massenlöschung
  Models.cs                           DTOs aus PROTOCOL.md
  PairingService.cs                   Code, Token, HMAC
  CertificateManager.cs               selbstsigniertes Zertifikat erzeugen/laden, Fingerabdruck
  Settings.cs                         settings.json in %LOCALAPPDATA%\RatioBackup
  ApiServer.cs                        Kestrel Minimal API
  DiscoveryResponder.cs               UDP-Antwort
pc/RatioBackup.Core.Tests/            xUnit
pc/RatioBackup.Tray/                  net8.0-windows WinForms, NotifyIcon, Einrichtung, Autostart
android/ (Gradle-Projekt wie ratioAI/Jarvis)
  app/src/main/java/de/ratio/backup/
    scan/PathFilter.kt, scan/Scanner.kt
    schedule/Schedule.kt, schedule/AlarmScheduler.kt, schedule/BootReceiver.kt
    net/Discovery.kt, net/PinnedClient.kt, net/BackupApi.kt, net/Pairing.kt
    backup/BackupRunner.kt, backup/BackupService.kt, backup/Prefs.kt, backup/Notifier.kt
    ui/MainActivity.kt, ui/MainScreen.kt, ui/SetupScreen.kt, ui/Permissions.kt
  app/src/test/java/de/ratio/backup/  JUnit-Tests (JVM)
```

---

### Task 1: Repo-Gerüst, Protokoll und CI

**Files:**
- Create: `docs/PROTOCOL.md`, `.github/workflows/build.yml`, `.gitignore`, `README.md`
- Create: `pc/RatioBackup.sln`, `pc/RatioBackup.Core/RatioBackup.Core.csproj`, `pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj`, `pc/RatioBackup.Tray/RatioBackup.Tray.csproj` (+ leeres `Program.cs`)
- Create: `android/` mit `settings.gradle.kts`, `build.gradle.kts`, `gradle/libs.versions.toml`, `gradle/wrapper/*`, `gradlew(.bat)`, `app/build.gradle.kts`, `app/src/main/AndroidManifest.xml`, Test-Keystore `android/keystore/ratiobackup-test.jks` (Passwort/Alias `ratiobackup`, wie bei Jarvis)
- Test: `pc/RatioBackup.Core.Tests/SmokeTest.cs`, `android/app/src/test/java/de/ratio/backup/SmokeTest.kt`

**Interfaces:**
- Produces: `docs/PROTOCOL.md` — der Vertrag, den Tasks 3–5 und 9 umsetzen:
  - UDP 47320: Handy sendet Broadcast `RATIOBACKUP?`; PC antwortet per Unicast mit JSON `{"name":string,"port":47321,"fingerprint":hex-SHA256 des Zertifikats}`.
  - `POST /pair` `{"code":"123456","deviceName":"Xiaomi 18 Pro"}` → `200 {"token":base64(32 Byte),"proof":hex(HMAC-SHA256(key=code, msg=fingerprint))}`; falscher/abgelaufener Code → `403`. Nach 5 Fehlversuchen wird der Code ungültig.
  - Alle anderen Endpunkte: Header `Authorization: Bearer <token>`, sonst `401`.
  - `GET /status` → `{"usedBytes","quotaBytes","trashBytes","lastBackupUtc"(ISO|null),"pendingDeletionReview":bool}`
  - `POST /manifest` `{"roots":[string],"files":[{"path","size","mtimeMs"}]}` → `{"needed":[{"path","offset"}],"trashed":int,"heldBackDeletions":int}`
  - `PUT /files?path=<url-encoded>&size=<long>&mtimeMs=<long>&offset=<long>` Body = Bytes ab `offset`, Header `X-Sha256` nur im Request, der die Datei vollständig macht → `200` fertig, `206` Teil gespeichert, `409 {"offset"}` falscher Offset, `422` Prüfsumme/Größe falsch (Teil verworfen), `507` Limit erreicht.
  - `POST /complete` → `200`; setzt `lastBackupUtc`, leert Papierkorb-Tage älter als 30.
- Produces: Workflow-Jobs `android` (ubuntu, JDK 17: `./gradlew testDebugUnitTest assembleDebug`), `pc-test` (ubuntu: `dotnet test pc/RatioBackup.sln`), `pc-exe` (windows-latest: `dotnet publish pc/RatioBackup.Tray -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`), `release` (lädt `RatioBackup.apk` und `RatioBackup-PC.exe` mit `--clobber` in Release `test`), `report` (läuft immer, schreibt JUnit-/TRX-Zusammenfassung als `results.md` in Branch `ci-results`, damit die Ergebnisse per `git fetch origin ci-results` lesbar sind).

- [ ] **Step 1:** Smoke-Tests anlegen (`Assert.True(true)` / `assertTrue(true)`), Projekte und Workflow wie oben erstellen; Gradle-/Versionsdateien von `ratioAI/Jarvis` übernehmen und um OkHttp `4.12.0`, kotlinx-serialization `1.9.0` (+ Plugin) ergänzen; Porcupine weglassen.
- [ ] **Step 2:** Commit + Push auf `main`.
- [ ] **Step 3:** `git fetch origin ci-results && git show origin/ci-results:results.md` — Erwartet: alle Jobs grün, Release `test` enthält beide Dateien.

---

### Task 2: PathMapper (PC)

**Files:**
- Create: `pc/RatioBackup.Core/PathMapper.cs`
- Test: `pc/RatioBackup.Core.Tests/PathMapperTests.cs`

**Interfaces:**
- Produces: `static class PathMapper { static string ToWindowsRelative(string phonePath); static bool IsSafe(string phonePath); }` — `phonePath` nutzt `/`; Rückgabe nutzt `\`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Plain() => Assert.Equal(@"DCIM\Camera\IMG_1.jpg", PathMapper.ToWindowsRelative("DCIM/Camera/IMG_1.jpg"));
[Fact] public void EscapesInvalidChars() => Assert.Equal(@"Download\a%3Ab%3F.txt", PathMapper.ToWindowsRelative("Download/a:b?.txt"));
[Fact] public void EscapesPercentItself() => Assert.Equal(@"Download\100%25.txt", PathMapper.ToWindowsRelative("Download/100%.txt"));
[Fact] public void EscapesTrailingDotAndSpace() => Assert.Equal(@"Download\x%2E\y%20", PathMapper.ToWindowsRelative("Download/x./y "));
[Fact] public void EscapesReservedNames() => Assert.Equal(@"Download\%43ON.txt", PathMapper.ToWindowsRelative("Download/CON.txt"));
[Theory, InlineData("../x"), InlineData("/abs"), InlineData("DCIM/../../x"), InlineData(""), InlineData("DCIM//x"), InlineData("C:/x")]
public void RejectsUnsafe(string p) => Assert.False(PathMapper.IsSafe(p));
```

- [ ] **Step 2:** `dotnet test --filter PathMapperTests` → FAIL (Typ fehlt) — lokal nicht möglich, also Push und `ci-results` prüfen.
- [ ] **Step 3:** Implementieren. Escaping: `%` + zwei Hex-Großbuchstaben des Zeichens; reservierte Gerätenamen (`CON PRN AUX NUL COM1-9 LPT1-9`, ohne Rücksicht auf Groß-/Kleinschreibung und Endung) → erstes Zeichen escapen.
- [ ] **Step 4:** Push → `ci-results`: PathMapperTests PASS.
- [ ] **Step 5:** Commit `feat(pc): PathMapper`.

---

### Task 3: BackupStore (PC)

**Files:**
- Create: `pc/RatioBackup.Core/BackupStore.cs`, `pc/RatioBackup.Core/Models.cs`
- Test: `pc/RatioBackup.Core.Tests/BackupStoreTests.cs`

**Interfaces:**
- Consumes: `PathMapper` (Task 2)
- Produces:
  - Records in `Models.cs`: `FileEntry(string Path, long Size, long MtimeMs)`, `ManifestRequest(List<string> Roots, List<FileEntry> Files)`, `NeededFile(string Path, long Offset)`, `ManifestResult(List<NeededFile> Needed, int Trashed, int HeldBackDeletions)`, `StatusDto(long UsedBytes, long QuotaBytes, long TrashBytes, DateTime? LastBackupUtc, bool PendingDeletionReview)`.
  - `enum UploadOutcome { Complete, Partial, WrongOffset, Corrupt, QuotaExceeded }`, `record UploadResult(UploadOutcome Outcome, long Offset)`.
  - `class BackupStore(string root, string deviceName, Func<DateTime> utcNow)` mit `long QuotaBytes {get;set;}` (Standard `100L*1024*1024*1024`), `ManifestResult ApplyManifest(ManifestRequest m)`, `Task<UploadResult> WriteAsync(string path, long size, long mtimeMs, long offset, Stream body, string? sha256Hex)`, `void Complete()`, `StatusDto GetStatus()`, `IReadOnlyList<string> HeldBackDeletions {get;}`, `void ConfirmHeldBackDeletions()`, `void DiscardHeldBackDeletions()`, `event Action<double>? QuotaWarning` (Anteil ≥ 0.9).
  - Ablage: Dateien `<root>\<deviceName>\…`, Teile `<root>\.ratiobackup\parts\<sha1(path)>.part`, Index `<root>\.ratiobackup\index.json` (Handy-Pfad → gespeicherter Windows-Pfad, Größe, mtime), Papierkorb `<root>\_Papierkorb\yyyy-MM-dd\…`.

- [ ] **Step 1: Failing tests** (temporärer Ordner, feste Uhr)

```csharp
NewFile_IsNeededWithOffsetZero        // Manifest mit a.jpg → Needed == [("DCIM/a.jpg",0)]
UnchangedFile_NotNeeded               // nach WriteAsync Complete + gleicher Eintrag → Needed leer
ChangedMtime_IsNeeded
Write_CompletesAndSetsMtime           // File.GetLastWriteTimeUtc == mtimeMs
Write_InTwoChunks_ResumesAtOffset     // 1. Teil → Partial(offset=n); Manifest danach → Needed offset == n; 2. Teil + sha → Complete
Write_WrongOffset_Returns409Offset    // offset ≠ Teilgröße → WrongOffset mit tatsächlichem Offset
Write_ShaMismatch_DiscardsPart        // → Corrupt, .part gelöscht, Manifest → offset 0
Write_SizeMismatch_DiscardsPart       // Bytes > size → Corrupt
PartialFile_NeverVisibleInDeviceFolder
MissingFile_MovedToTrashWithDate      // existiert unter _Papierkorb\2026-11-26\DCIM\a.jpg; Trashed == 1
Deletion_OnlyWithinListedRoots        // Datei unter "Download" bleibt, wenn Roots nur "DCIM" enthält
MassDeletion_HeldBack                 // 100 Dateien in DCIM, Manifest mit 70 → Trashed 0, HeldBackDeletions 30, Status.PendingDeletionReview true
MassDeletion_BelowFloor_Applied       // 10 Dateien, Manifest mit 0 → Trashed 10 (unter 50 greift der Schutz nicht)
ConfirmHeldBack_MovesToTrash
TrashOlderThan30Days_PurgedOnComplete // Ordner 2026-10-26 gelöscht, 2026-10-27 bleibt (heute 2026-11-26)
CaseCollision_BothKept                // "DCIM/IMG.jpg" und "DCIM/img.JPG" → zwei verschiedene Dateien, zweite als "img~2.JPG"
Quota_PurgesTrashFirst                // Limit knapp, Papierkorb voll → Upload gelingt, Papierkorb leer
Quota_ExceededAfterPurge_Returns507   // QuotaExceeded, keine Datei im Geräteordner verändert
QuotaWarning_FiresAt90Percent
```

- [ ] **Step 2:** Push → `ci-results`: FAIL.
- [ ] **Step 3:** Implementieren. Schutzregel: zurückhalten, wenn für eine Wurzel `fehlend ≥ 50` **und** `fehlend / bekannt > 0.2`. Limit-Prüfung vor jedem Schreiben: `used + (size - offset) > QuotaBytes` → Papierkorb komplett leeren, erneut prüfen, sonst `QuotaExceeded`. Index nach jeder fertigen Datei atomar schreiben (temp + `File.Move(overwrite:true)`).
- [ ] **Step 4:** Push → alle BackupStoreTests PASS.
- [ ] **Step 5:** Commit `feat(pc): BackupStore`.

---

### Task 4: Kopplung, Zertifikat, Einstellungen (PC)

**Files:**
- Create: `pc/RatioBackup.Core/PairingService.cs`, `CertificateManager.cs`, `Settings.cs`
- Test: `pc/RatioBackup.Core.Tests/PairingTests.cs`, `CertificateTests.cs`, `SettingsTests.cs`

**Interfaces:**
- Produces:
  - `class Settings { string? StorageRoot; int QuotaGb = 100; string DeviceName = "Xiaomi 18 Pro"; string? TokenSha256; bool Autostart = true; static Settings Load(string dir); void Save(string dir); }`
  - `class CertificateManager { static X509Certificate2 LoadOrCreate(string dir); static string Fingerprint(X509Certificate2 c); }` — RSA 2048, CN=`RatioBackup`, 20 Jahre gültig, gespeichert als `server.pfx`; Fingerabdruck = SHA-256 über DER, Kleinbuchstaben-Hex.
  - `class PairingService(Settings s, string fingerprint, Func<DateTime> utcNow) { string NewCode(); PairResult? TryPair(string code, string deviceName); bool IsAuthorized(string? bearer); event Action? Paired; }`, `record PairResult(string Token, string Proof)`.

- [ ] **Step 1: Failing tests:** `NewCode_IsSixDigits`; `TryPair_CorrectCode_ReturnsTokenAndProof` (Proof == HMAC-SHA256(code, fingerprint) hex); `TryPair_AfterFiveMinutes_Null`; `TryPair_SixthAttemptAfterFiveWrong_NullEvenIfCorrect`; `CodeSingleUse`; `IsAuthorized_OnlyIssuedToken` (gespeichert wird nur `TokenSha256`); `NewPairing_InvalidatesOldToken`; `Certificate_PersistsSameFingerprint`; `Settings_RoundTrip`.
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren (`RandomNumberGenerator.GetInt32(0, 1_000_000)` → `D6`; Vergleich mit `CryptographicOperations.FixedTimeEquals`).
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(pc): Kopplung und Zertifikat`.

---

### Task 5: API-Server und Gerätesuche (PC)

**Files:**
- Create: `pc/RatioBackup.Core/ApiServer.cs`, `DiscoveryResponder.cs`
- Test: `pc/RatioBackup.Core.Tests/ApiTests.cs` (TestHost), `DiscoveryTests.cs`

**Interfaces:**
- Consumes: `BackupStore`, `PairingService`, `CertificateManager`, Models.
- Produces: `static class ApiServer { static WebApplication Build(BackupStore store, PairingService pairing, X509Certificate2? cert, int port = 47321); }` (cert `null` nur für Tests mit TestServer); `class DiscoveryResponder(string name, int apiPort, string fingerprint) : IDisposable { void Start(int udpPort = 47320); }`.

- [ ] **Step 1: Failing tests:** `Pair_WrongCode_403`; `Status_WithoutToken_401`; `Manifest_ThenUpload_ThenStatusShowsUsedBytes`; `Upload_Partial_Returns206_ThenResume_200`; `Upload_WrongOffset_409WithOffsetBody`; `Upload_BadSha_422`; `Upload_OverQuota_507`; `Upload_UnsafePath_400` (`../x`); `Complete_SetsLastBackup`; `Discovery_RepliesJsonToProbe` (UDP auf `127.0.0.1`, freier Port).
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren. Kestrel lauscht auf `0.0.0.0:47321` mit dem Zertifikat; Request-Body-Limit aus (`MaxRequestBodySize = null`), Body wird direkt in die `.part`-Datei gestreamt.
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(pc): API und Gerätesuche`.

---

### Task 6: Taskleistenprogramm (PC)

**Files:**
- Create: `pc/RatioBackup.Tray/Program.cs`, `TrayApp.cs`, `SetupForm.cs`, `PairingForm.cs`, `Autostart.cs`, `Resources/icon.ico`

**Interfaces:**
- Consumes: alles aus `RatioBackup.Core`.

- [ ] **Step 1:** Erster Start (keine `StorageRoot`): `SetupForm` mit Ordnerauswahl (Hinweis „am besten nicht Laufwerk C:"), Speicherlimit (Standard 100 GB), danach `PairingForm` mit großem 6-stelligem Code und Hinweis „Windows fragt gleich nach der Firewall – bitte für **private Netzwerke** zulassen".
- [ ] **Step 2:** `TrayApp`: NotifyIcon mit Tooltip „Letzte Sicherung: …"; Menü: Status, Ordner öffnen, Handy neu koppeln, Speicherlimit ändern, Autostart an/aus, Beenden. Sprechblasen: Limit ≥ 90 %, Limit voll, zurückgehaltene Löschungen (Klick → Dialog mit Anzahl und Beispielpfaden, Knöpfe „In Papierkorb verschieben" / „Behalten").
- [ ] **Step 3:** `Autostart`: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RatioBackup` = Pfad der EXE; nur eine Instanz (benannter Mutex `RatioBackup.Tray`).
- [ ] **Step 4:** Push → `ci-results`: `pc-exe` grün, `RatioBackup-PC.exe` im Release.
- [ ] **Step 5:** Commit `feat(pc): Taskleistenprogramm`. (Oberfläche wird in Task 12 am echten PC geprüft.)

---

### Task 7: Dateisuche am Handy

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/scan/PathFilter.kt`, `scan/Scanner.kt`
- Test: `android/app/src/test/java/de/ratio/backup/ScannerTest.kt`

**Interfaces:**
- Produces: `@Serializable data class FileEntry(val path: String, val size: Long, val mtimeMs: Long)`; `object PathFilter { val ROOTS: List<String>; fun include(relPath: String): Boolean }`; `class Scanner(private val storageRoot: File) { fun scan(): ScanResult }`, `data class ScanResult(val roots: List<String>, val files: List<FileEntry>)` — `roots` enthält nur Wurzeln, die existieren **und** lesbar sind.

- [ ] **Step 1: Failing tests (TemporaryFolder):** `scan_findsFilesUnderRoots`; `scan_ignoresOtherFolders` (`Documents/x`); `filter_excludesThumbnailsTrashedPendingAndNomedia` (`.thumbnails/`, `.trashed-…`, `.pending-…`, `.nomedia`); `scan_missingRoot_notInRoots` (WhatsApp nicht installiert → Wurzel fehlt in `roots`, damit der PC dort nichts löscht); `paths_useForwardSlash_relativeToStorageRoot`.
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren mit `File.walkTopDown()`, Symlinks nicht folgen.
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(android): Scanner`.

---

### Task 8: Zeitplan-Logik

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/schedule/Schedule.kt`
- Test: `android/app/src/test/java/de/ratio/backup/ScheduleTest.kt`

**Interfaces:**
- Produces: `object Schedule { val START: LocalTime = 20:00; val END: LocalTime = 20:30; val RETRY: Duration = 5 min; fun nextAttempt(now: ZonedDateTime, lastSuccessDate: LocalDate?): ZonedDateTime; fun needsStaleWarning(now: ZonedDateTime, lastSuccess: Instant?): Boolean /* > 3 Tage */ }`

- [ ] **Step 1: Failing tests** (Zone `Europe/Berlin`):

```kotlin
before20_today2000            // 14:00 → heute 20:00
inWindow_noSuccess_plus5      // 20:07 → 20:12
inWindow_lastRetryAt2030      // 20:27 → 20:30 (letzter Versuch genau 20:30)
after2030_tomorrow2000        // 20:31 → morgen 20:00
successToday_tomorrow2000     // 20:05, lastSuccess heute → morgen 20:00
at2359_tomorrow2000
dstSpring_2027_03_28          // 27.03. 21:00 → 28.03. 20:00 MESZ
dstAutumn_2027_10_31
staleWarning_after3Days       // true bei 3 Tagen + 1 min, false bei 2 Tagen 23 h
```

- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren (reine Funktion, keine Android-Abhängigkeit).
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(android): Zeitplan`.

---

### Task 9: Netzwerk-Client (Handy)

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/net/Discovery.kt`, `PinnedClient.kt`, `Pairing.kt`, `BackupApi.kt`
- Test: `android/app/src/test/java/de/ratio/backup/PairingTest.kt`, `BackupApiTest.kt` (OkHttp `mockwebserver` 4.12.0)

**Interfaces:**
- Consumes: `FileEntry` (Task 7), `docs/PROTOCOL.md`.
- Produces:
  - `data class PcInfo(val host: String, val port: Int, val name: String, val fingerprint: String)`; `class Discovery(context: Context) { suspend fun find(timeoutMs: Long = 3000): PcInfo? }` (Broadcast an `255.255.255.255:47320`, hält `WifiManager.MulticastLock`).
  - `object PinnedClient { fun create(fingerprint: String): OkHttpClient }` — akzeptiert nur ein Zertifikat mit genau diesem SHA-256, Hostname-Prüfung aus (IP im LAN).
  - `object Pairing { fun verifyProof(code: String, fingerprint: String, proof: String): Boolean; suspend fun pair(pc: PcInfo, code: String, deviceName: String): String /* token */ }` — wirft `PairingException("Falscher Code")` bei 403 oder ungültigem Proof.
  - `class BackupApi(client: OkHttpClient, baseUrl: HttpUrl, token: String) { suspend fun status(): Status; suspend fun manifest(roots: List<String>, files: List<FileEntry>): ManifestResult; suspend fun upload(file: File, entry: FileEntry, offset: Long): UploadResult; suspend fun complete() }`, `sealed interface UploadResult { Done; Partial(offset); WrongOffset(offset); Corrupt; QuotaFull }`. `upload` streamt ab `offset`, berechnet SHA-256 über die ganze Datei und sendet ihn mit `X-Sha256`.
- [ ] **Step 1: Failing tests:** `verifyProof_matchesPcHmac` (fester Code/Fingerabdruck, erwarteter Hex-Wert aus C#-Test in Task 4 übernommen); `pair_403_throws`; `upload_sendsFromOffset` (Server prüft Body-Länge = size − offset und `offset`-Query); `upload_409_returnsWrongOffset`; `upload_507_returnsQuotaFull`; `upload_422_returnsCorrupt`; `requests_carryBearerToken`.
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren (kotlinx.serialization für JSON; Timeouts: connect 5 s, read/write 60 s).
- [ ] **Step 4:** Push → PASS.
- [ ] **Step 5:** Commit `feat(android): Netzwerk-Client`.

---

### Task 10: Sicherungsablauf, Dienst, Wecker, Benachrichtigungen

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/backup/Prefs.kt`, `BackupRunner.kt`, `BackupService.kt`, `Notifier.kt`, `schedule/AlarmScheduler.kt`, `schedule/BootReceiver.kt`
- Modify: `android/app/src/main/AndroidManifest.xml`
- Test: `android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt`

**Interfaces:**
- Consumes: `Scanner`, `Schedule`, `Discovery`, `BackupApi`, `PinnedClient`.
- Produces:
  - `class Prefs(context) { var token: String?; var fingerprint: String?; var lastHost: String?; var lastSuccess: Instant?; var lastStatus: String? }`
  - `class BackupRunner(scan: () -> ScanResult, api: BackupApiPort, open: (FileEntry) -> File) { suspend fun run(onProgress: (done: Int, total: Int) -> Unit): RunOutcome }`, `interface BackupApiPort` (die vier Methoden von `BackupApi`, damit der Test ein Fake nutzen kann), `sealed interface RunOutcome { Success; PcNotFound; QuotaFull; Failed(reason) }`.
  - `object AlarmScheduler { fun scheduleNext(context: Context) }` — `setExactAndAllowWhileIdle` auf `Schedule.nextAttempt`; Wecker startet `BackupService`.
  - `BackupService`: Foreground-Service `foregroundServiceType="dataSync"`, Benachrichtigung „Sichere … (x von y)", ruft `BackupRunner`, setzt `lastSuccess` bei Erfolg, plant immer neu, ruft `Notifier.maybeStaleWarning`.
  - Manifest-Rechte: `MANAGE_EXTERNAL_STORAGE`, `INTERNET`, `ACCESS_WIFI_STATE`, `CHANGE_WIFI_MULTICAST_STATE`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_DATA_SYNC`, `POST_NOTIFICATIONS`, `USE_EXACT_ALARM`, `RECEIVE_BOOT_COMPLETED`, `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`.
- [ ] **Step 1: Failing tests (Fake-API):** `uploadsOnlyNeeded_thenComplete`; `resumesFromOffsetGivenByManifest`; `wrongOffset_retriesOnceFromServerOffset`; `corrupt_skipsFile_runStillCompletes` (Datei kommt nächstes Mal wieder); `quotaFull_stopsWithoutComplete`; `networkError_returnsFailed_noComplete`.
- [ ] **Step 2:** Push → FAIL.
- [ ] **Step 3:** Implementieren; `BootReceiver` und App-Start rufen `AlarmScheduler.scheduleNext`.
- [ ] **Step 4:** Push → PASS, APK baut.
- [ ] **Step 5:** Commit `feat(android): Sicherungsablauf und Zeitplanung`.

---

### Task 11: Oberfläche und Einrichtung (Handy)

**Files:**
- Create: `android/app/src/main/java/de/ratio/backup/ui/MainActivity.kt`, `MainScreen.kt`, `SetupScreen.kt`, `Permissions.kt`

**Interfaces:**
- Consumes: `Prefs`, `Discovery`, `Pairing`, `BackupApi.status`, `AlarmScheduler`, `BackupService`.

- [ ] **Step 1:** `SetupScreen` als Schrittfolge, jeder Schritt mit Haken, sobald erledigt: 1) „Zugriff auf alle Dateien" (`ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION`), 2) Benachrichtigungen, 3) Akku „Keine Einschränkungen" (`ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`), 4) Xiaomi-Autostart (`com.miui.securitycenter/com.miui.permcenter.autostart.AutoStartManagementActivity`, Rückfall: App-Info-Seite) — nicht prüfbar, daher Knopf „Erledigt", 5) PC suchen + 6-stelligen Code eingeben.
- [ ] **Step 2:** `MainScreen`: „Letzte Sicherung", „Nächste Sicherung" (aus `Schedule.nextAttempt`), „PC-Speicher: x von y GB" (aus `/status`, sonst „PC nicht erreichbar"), Hinweis bei zurückgehaltenen Löschungen („Bitte am PC bestätigen"), Knopf „Jetzt sichern".
- [ ] **Step 3:** Push → APK baut, Release enthält `RatioBackup.apk`.
- [ ] **Step 4:** Commit `feat(android): Oberfläche und Einrichtung`.

---

### Task 12: Test mit Alexanders Geräten

- [ ] **Step 1:** Alexander installiert `RatioBackup-PC.exe`, wählt den Ordner, lässt die Firewall für private Netzwerke zu.
- [ ] **Step 2:** Alexander installiert `RatioBackup.apk`, geht die Einrichtung durch, koppelt mit dem Code.
- [ ] **Step 3:** „Jetzt sichern" → Erwartet: Fortschritt am Handy, Ordner `Xiaomi 18 Pro\DCIM\…`, `Download`, `Android\media\com.whatsapp\…` am PC, Status zeigt den belegten Platz.
- [ ] **Step 4:** Testfoto löschen, erneut sichern → Erwartet: Foto in `_Papierkorb\<heute>\…`.
- [ ] **Step 5:** Während eines großen Videos das WLAN ausschalten, wieder einschalten, „Jetzt sichern" → Erwartet: Es geht weiter statt von vorn.
- [ ] **Step 6:** Am nächsten Abend nach 20 Uhr prüfen: Läuft die automatische Sicherung? (Hier zeigt sich, ob HyperOS die App in Ruhe lässt.)
