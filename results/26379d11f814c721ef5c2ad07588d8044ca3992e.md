# 26379d11f814c721ef5c2ad07588d8044ca3992e (dev)
## android: failure
Tests: 0 bestanden, 0 fehlgeschlagen
Fehlerzeilen:
```
> Task :app:checkKotlinGradlePluginConfigurationErrors SKIPPED
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:6:28 Unresolved reference 'FileChangedException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:7:28 Unresolved reference 'PcErrorException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:131:68 Unresolved reference 'FileChangedException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:137:68 Unresolved reference 'PcErrorException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:145:64 No parameter with name 'hasStorageAccess' found.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:146:33 Unresolved reference 'NoStorageAccess'.
> Task :app:compileDebugUnitTestKotlin FAILED
FAILURE: Build failed with an exception.
* What went wrong:
Execution failed for task ':app:compileDebugUnitTestKotlin'.
   > Compilation error. See log for more details
BUILD FAILED in 59s
```
Logende:
```

> Task :app:stripDebugDebugSymbols
Unable to strip the following libraries, packaging them as they are: libandroidx.graphics.path.so. Run with --info option to learn more.

> Task :app:validateSigningDebug
> Task :app:writeDebugAppMetadata
> Task :app:writeDebugSigningConfigVersions
> Task :app:compileDebugKotlin
> Task :app:compileDebugJavaWithJavac NO-SOURCE
> Task :app:bundleDebugClassesToRuntimeJar
> Task :app:bundleDebugClassesToCompileJar
> Task :app:processDebugJavaRes

> Task :app:compileDebugUnitTestKotlin
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:6:28 Unresolved reference 'FileChangedException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:7:28 Unresolved reference 'PcErrorException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:131:68 Unresolved reference 'FileChangedException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:137:68 Unresolved reference 'PcErrorException'.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:145:64 No parameter with name 'hasStorageAccess' found.
e: file:///home/runner/work/ratioBackup/ratioBackup/android/app/src/test/java/de/ratio/backup/BackupRunnerTest.kt:146:33 Unresolved reference 'NoStorageAccess'.

> Task :app:compileDebugUnitTestKotlin FAILED
> Task :app:dexBuilderDebug
gradle/actions: Writing build results to /home/runner/work/_temp/.gradle-actions/build-results/build-1795708010767.json

FAILURE: Build failed with an exception.

* What went wrong:
Execution failed for task ':app:compileDebugUnitTestKotlin'.
> A failure occurred while executing org.jetbrains.kotlin.compilerRunner.GradleCompilerRunnerWithWorkers$GradleKotlinCompilerWorkAction
   > Compilation error. See log for more details

* Try:
> Run with --stacktrace option to get the stack trace.
> Run with --info or --debug option to get more log output.
> Run with --scan to get full insights.
> Get more help at https://help.gradle.org.

BUILD FAILED in 59s
36 actionable tasks: 36 executed
```
## pc-test: failure
Tests: 0 bestanden, 0 fehlgeschlagen
Fehlerzeilen:
```
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(326,23): error CS1061: 'BackupStore' does not contain a definition for 'SaveCount' and no accessible extension method 'SaveCount' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(326,56): error CS1061: 'BackupStore' does not contain a definition for 'SaveCount' and no accessible extension method 'SaveCount' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(336,11): error CS1061: 'BackupStore' does not contain a definition for 'Flush' and no accessible extension method 'Flush' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
```
Logende:
```
  Determining projects to restore...
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/RatioBackup.Core.csproj (in 117 ms).
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj (in 2.37 sec).
  RatioBackup.Core -> /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/bin/Debug/net8.0/RatioBackup.Core.dll
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(326,23): error CS1061: 'BackupStore' does not contain a definition for 'SaveCount' and no accessible extension method 'SaveCount' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(326,56): error CS1061: 'BackupStore' does not contain a definition for 'SaveCount' and no accessible extension method 'SaveCount' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs(336,11): error CS1061: 'BackupStore' does not contain a definition for 'Flush' and no accessible extension method 'Flush' accepting a first argument of type 'BackupStore' could be found (are you missing a using directive or an assembly reference?) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/ApiTests.cs(164,47): warning xUnit1031: Test methods should not use blocking task operations, as they can cause deadlocks. Use an async test method and await instead. (https://xunit.net/xunit.analyzers/rules/xUnit1031) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
```
## pc-exe: success
Tests: 0 bestanden, 0 fehlgeschlagen
