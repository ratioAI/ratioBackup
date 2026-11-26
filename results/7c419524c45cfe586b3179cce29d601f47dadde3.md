# 7c419524c45cfe586b3179cce29d601f47dadde3 (dev)
## android: success
Tests: 72 bestanden, 0 fehlgeschlagen
Signatur:
```
Verified using v1 scheme (JAR signing): false
Verified using v2 scheme (APK Signature Scheme v2): true
Verified using v3 scheme (APK Signature Scheme v3): false
Verified using v3.1 scheme (APK Signature Scheme v3.1): false
Verified using v3.2 scheme (APK Signature Scheme v3.2): false
Verified using v4 scheme (APK Signature Scheme v4): false
```
## pc-test: failure
Tests: 92 bestanden, 2 fehlgeschlagen
- FAIL RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreRunTests.cs:line 57
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
```
- FAIL RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate
```
Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: [4, 4, 4, 4, 4]
Actual:   [3, 3, 3, 3, 3]
           ↑ (pos 0)
   at RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreRunTests.cs:line 68
--- End of stack trace from previous location ---
```
Fehlerzeilen:
```
  Failed RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate [11 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes [2 ms]
  Error Message:
Failed!  - Failed:     2, Passed:    92, Skipped:     0, Total:    94, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
Logende:
```
  Determining projects to restore...
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/RatioBackup.Core.csproj (in 60 ms).
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj (in 1.21 sec).
  RatioBackup.Core -> /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/bin/Debug/net8.0/RatioBackup.Core.dll
/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/ApiTests.cs(164,47): warning xUnit1031: Test methods should not use blocking task operations, as they can cause deadlocks. Use an async test method and await instead. (https://xunit.net/xunit.analyzers/rules/xUnit1031) [/home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj]
  RatioBackup.Core.Tests -> /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/bin/Debug/net8.0/RatioBackup.Core.Tests.dll
Test run for /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/bin/Debug/net8.0/RatioBackup.Core.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.73]     RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate [FAIL]
  Failed RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate [11 ms]
  Error Message:
   Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: [4, 4, 4, 4, 4]
Actual:   [3, 3, 3, 3, 3]
           ↑ (pos 0)
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreRunTests.Upload_UntrackedFileOnDisk_OverwritesNotDuplicate() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreRunTests.cs:line 68
--- End of stack trace from previous location ---
[xUnit.net 00:00:00.74]     RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes [FAIL]
  Failed RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes [2 ms]
  Error Message:
   Assert.True() Failure
Expected: True
Actual:   False
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreRunTests.Run_SurvivesLongPauseWhilePhoneHashes() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreRunTests.cs:line 57
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Results File: /home/runner/work/ratioBackup/ratioBackup/pc-results/_ip-10-0-132-134_2026-11-26_20_18_21_net8.0.trx

Failed!  - Failed:     2, Passed:    92, Skipped:     0, Total:    94, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
## pc-exe: success
Tests: 0 bestanden, 0 fehlgeschlagen
