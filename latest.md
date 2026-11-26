# f1d008ec4709ae159ed060e2ed281cd83cf59965 (dev)
## android: success
Tests: 1 bestanden, 0 fehlgeschlagen
## pc-test: failure
Tests: 42 bestanden, 1 fehlgeschlagen
- FAIL RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 208
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
```
Fehlerzeilen:
```
  Failed RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete [8 ms]
  Error Message:
Failed!  - Failed:     1, Passed:    42, Skipped:     0, Total:    43, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
Logende:
```
  Determining projects to restore...
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/RatioBackup.Core.csproj (in 101 ms).
  Restored /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/RatioBackup.Core.Tests.csproj (in 2.98 sec).
  RatioBackup.Core -> /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core/bin/Debug/net8.0/RatioBackup.Core.dll
  RatioBackup.Core.Tests -> /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/bin/Debug/net8.0/RatioBackup.Core.Tests.dll
Test run for /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/bin/Debug/net8.0/RatioBackup.Core.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.83]     RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete [FAIL]
  Failed RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete [8 ms]
  Error Message:
   Assert.True() Failure
Expected: True
Actual:   False
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 208
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
Results File: /home/runner/work/ratioBackup/ratioBackup/pc-results/_runnervm0hi8g_2026-11-26_15_02_30_net8.0.trx

Failed!  - Failed:     1, Passed:    42, Skipped:     0, Total:    43, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
## pc-exe: success
Tests: 0 bestanden, 0 fehlgeschlagen
