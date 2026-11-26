# 39d3ccd632803343fce1c612648ca254f6fa41cf (dev)
## android: success
Tests: 1 bestanden, 0 fehlgeschlagen
## pc-test: failure
Tests: 33 bestanden, 10 fehlgeschlagen
- FAIL RatioBackup.Core.Tests.BackupStoreTests.ConfirmHeldBack_MovesToTrash
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.ConfirmHeldBack_MovesToTrash() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 184
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 208
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 140
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.Write_CompletesAndSetsMtime
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.Write_CompletesAndSetsMtime() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 57
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.DiscardHeldBack_KeepsFiles
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.DiscardHeldBack_KeepsFiles() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 195
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack
```
Assert.Equal() Failure: Values differ
Expected: 0
Actual:   30
   at RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 159
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.CaseCollision_BothKept
```
System.IO.DirectoryNotFoundException : Could not find a part of the path '/tmp/rb-4f8f0cee1dfa4842b6e09dc36bf5ea3d/Xiaomi 18 Pro/DCIM/IMG.jpg'.
   at Interop.ThrowExceptionForIoErrno(ErrorInfo errorInfo, String path, Boolean isDirError)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String path, OpenFlags flags, Int32 mode, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, UnixFileMode openPermissions, Int64& fileLength, UnixFileMode& filePermissions, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at System.IO.F
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.Quota_ExceededAfterPurge_ReturnsQuotaExceeded
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.Quota_ExceededAfterPurge_ReturnsQuotaExceeded() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 250
--- End of stack trace from previous location ---
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.Write_InTwoChunks_ResumesAtOffset
```
System.IO.DirectoryNotFoundException : Could not find a part of the path '/tmp/rb-3916f8ce8b694fbcb11d17935ff1ee0e/Xiaomi 18 Pro/DCIM/v.mp4'.
   at Interop.ThrowExceptionForIoErrno(ErrorInfo errorInfo, String path, Boolean isDirError)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String path, OpenFlags flags, Int32 mode, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, UnixFileMode openPermissions, Int64& fileLength, UnixFileMode& filePermissions, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at System.IO.F
```
- FAIL RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate
```
Assert.True() Failure
Expected: True
Actual:   False
   at RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 130
--- End of stack trace from previous location ---
```
Fehlerzeilen:
```
  Failed RatioBackup.Core.Tests.BackupStoreTests.TrashOlderThan30Days_PurgedOnComplete [25 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.CaseCollision_BothKept [517 ms]
  Error Message:
   System.IO.DirectoryNotFoundException : Could not find a part of the path '/tmp/rb-4f8f0cee1dfa4842b6e09dc36bf5ea3d/Xiaomi 18 Pro/DCIM/IMG.jpg'.
     at Interop.ThrowExceptionForIoErrno(ErrorInfo errorInfo, String path, Boolean isDirError)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String path, OpenFlags flags, Int32 mode, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, UnixFileMode openPermissions, Int64& fileLength, UnixFileMode& filePermissions, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
  Failed RatioBackup.Core.Tests.BackupStoreTests.Quota_ExceededAfterPurge_ReturnsQuotaExceeded [5 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.Write_CompletesAndSetsMtime [5 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.Write_InTwoChunks_ResumesAtOffset [13 ms]
  Error Message:
   System.IO.DirectoryNotFoundException : Could not find a part of the path '/tmp/rb-3916f8ce8b694fbcb11d17935ff1ee0e/Xiaomi 18 Pro/DCIM/v.mp4'.
     at Interop.ThrowExceptionForIoErrno(ErrorInfo errorInfo, String path, Boolean isDirError)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String path, OpenFlags flags, Int32 mode, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, UnixFileMode openPermissions, Int64& fileLength, UnixFileMode& filePermissions, Boolean failForSymlink, Boolean& wasSymlink, Func`4 createOpenException)
  Failed RatioBackup.Core.Tests.BackupStoreTests.ConfirmHeldBack_MovesToTrash [74 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.DiscardHeldBack_KeepsFiles [68 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots [3 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack [117 ms]
  Error Message:
  Failed RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate [3 ms]
  Error Message:
Failed!  - Failed:    10, Passed:    33, Skipped:     0, Total:    43, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
Logende:
```
     at RatioBackup.Core.Tests.BackupStoreTests.ConfirmHeldBack_MovesToTrash() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 184
--- End of stack trace from previous location ---
  Failed RatioBackup.Core.Tests.BackupStoreTests.DiscardHeldBack_KeepsFiles [68 ms]
  Error Message:
   Assert.True() Failure
Expected: True
Actual:   False
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreTests.DiscardHeldBack_KeepsFiles() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 195
--- End of stack trace from previous location ---
[xUnit.net 00:00:02.62]     RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots [FAIL]
[xUnit.net 00:00:02.74]     RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack [FAIL]
[xUnit.net 00:00:02.76]     RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate [FAIL]
  Failed RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots [3 ms]
  Error Message:
   Assert.True() Failure
Expected: True
Actual:   False
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreTests.Deletion_OnlyWithinListedRoots() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 140
--- End of stack trace from previous location ---
  Failed RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack [117 ms]
  Error Message:
   Assert.Equal() Failure: Values differ
Expected: 0
Actual:   30
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreTests.MassDeletion_HeldBack() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 159
--- End of stack trace from previous location ---
  Failed RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate [3 ms]
  Error Message:
   Assert.True() Failure
Expected: True
Actual:   False
  Stack Trace:
     at RatioBackup.Core.Tests.BackupStoreTests.MissingFile_MovedToTrashWithDate() in /home/runner/work/ratioBackup/ratioBackup/pc/RatioBackup.Core.Tests/BackupStoreTests.cs:line 130
--- End of stack trace from previous location ---
Results File: /home/runner/work/ratioBackup/ratioBackup/pc-results/_runnervm3lb7s_2026-11-26_15_00_05_net8.0.trx

Failed!  - Failed:    10, Passed:    33, Skipped:     0, Total:    43, Duration: 1 s - RatioBackup.Core.Tests.dll (net8.0)
```
## pc-exe: success
Tests: 0 bestanden, 0 fehlgeschlagen
