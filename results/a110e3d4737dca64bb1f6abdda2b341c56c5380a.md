# a110e3d4737dca64bb1f6abdda2b341c56c5380a (dev)
## android: failure
Tests: 67 bestanden, 5 fehlgeschlagen
- FAIL de.ratio.backup.ScannerTest.scan_findsFilesUnderRoots
```
java.lang.AssertionError: expected:<[DCIM/Camera/a.jpg, Download/b.pdf, Android/media/com.whatsapp/WhatsApp/Media/WhatsApp Images/c.jpg, Android/media/com.whatsapp/WhatsApp/Databases/msgstore.db.crypt14]> but was:<[]>
java.lang.AssertionError: expected:<[DCIM/Camera/a.jpg, Download/b.pdf, Android/media/com.whatsapp/WhatsApp/Media/WhatsApp Images/c.jpg, Android/media/com.whatsapp/WhatsApp/Databases/msgstore.db.crypt14]> but was:<[]>
	at org.junit.Assert.fail(Assert.java:89)
	at org.junit.Assert.failNotEquals(Assert.java:835)
	at org.junit.Assert.assertEquals(Assert.java:120)
	at org.junit.Assert.assertEquals(Assert.java:146)
	at de.ratio.backup.ScannerTest.scan_findsFilesUnderRoots(ScannerTest.kt:32)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:77)
	at java.base/jdk.internal.reflect.DelegatingMethodAccessorImpl.invoke(DelegatingMethodAccessorImpl.java:43)
	at java.base/java.lang.reflect.Method.invoke(Method.java:569)
	at org.junit.runners.model.FrameworkMethod$1.runReflectiveCall(FrameworkMethod.java:59)
	at org.junit.internal.runners.model.Reflectiv
```
- FAIL de.ratio.backup.ScannerTest.paths_useForwardSlash_relativeToStorageRoot
```
java.util.NoSuchElementException: List is empty.
java.util.NoSuchElementException: List is empty.
	at kotlin.collections.CollectionsKt___CollectionsKt.single(_Collections.kt:616)
	at de.ratio.backup.ScannerTest.paths_useForwardSlash_relativeToStorageRoot(ScannerTest.kt:78)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:77)
	at java.base/jdk.internal.reflect.DelegatingMethodAccessorImpl.invoke(DelegatingMethodAccessorImpl.java:43)
	at java.base/java.lang.reflect.Method.invoke(Method.java:569)
	at org.junit.runners.model.FrameworkMethod$1.runReflectiveCall(FrameworkMethod.java:59)
	at org.junit.internal.runners.model.ReflectiveCallable.run(ReflectiveCallable.java:12)
	at org.junit.runners.model.FrameworkMethod.invokeExplosively(FrameworkMethod.java:56)
	at org.junit.internal.runners.statements.InvokeMethod.evaluate(InvokeMethod.java:17)
	at org.junit.rules.ExternalResource$1.evaluate(ExternalResource.java:54)
	at org.junit.runners.ParentRunner$3.evaluate(ParentRunner.java:306)
	at org.junit.runners.BlockJUnit4ClassRunner$1.evaluate(BlockJUnit4ClassRunne
```
- FAIL de.ratio.backup.ScannerTest.scan_missingRoot_notInRoots
```
java.lang.AssertionError
java.lang.AssertionError
	at org.junit.Assert.fail(Assert.java:87)
	at org.junit.Assert.assertTrue(Assert.java:42)
	at org.junit.Assert.assertTrue(Assert.java:53)
	at de.ratio.backup.ScannerTest.scan_missingRoot_notInRoots(ScannerTest.kt:65)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:77)
	at java.base/jdk.internal.reflect.DelegatingMethodAccessorImpl.invoke(DelegatingMethodAccessorImpl.java:43)
	at java.base/java.lang.reflect.Method.invoke(Method.java:569)
	at org.junit.runners.model.FrameworkMethod$1.runReflectiveCall(FrameworkMethod.java:59)
	at org.junit.internal.runners.model.ReflectiveCallable.run(ReflectiveCallable.java:12)
	at org.junit.runners.model.FrameworkMethod.invokeExplosively(FrameworkMethod.java:56)
	at org.junit.internal.runners.statements.InvokeMethod.evaluate(InvokeMethod.java:17)
	at org.junit.rules.ExternalResource$1.evaluate(ExternalResource.java:54)
	at org.junit.runners.ParentRunner$3.evaluate(ParentRunner.java:306)
	at org.junit.runners.BlockJUnit4ClassRunner$1.evaluate(BlockJUnit4ClassRunner.java:
```
- FAIL de.ratio.backup.ScannerTest.scan_emptyExistingRoot_isInRoots
```
java.lang.AssertionError
java.lang.AssertionError
	at org.junit.Assert.fail(Assert.java:87)
	at org.junit.Assert.assertTrue(Assert.java:42)
	at org.junit.Assert.assertTrue(Assert.java:53)
	at de.ratio.backup.ScannerTest.scan_emptyExistingRoot_isInRoots(ScannerTest.kt:72)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:77)
	at java.base/jdk.internal.reflect.DelegatingMethodAccessorImpl.invoke(DelegatingMethodAccessorImpl.java:43)
	at java.base/java.lang.reflect.Method.invoke(Method.java:569)
	at org.junit.runners.model.FrameworkMethod$1.runReflectiveCall(FrameworkMethod.java:59)
	at org.junit.internal.runners.model.ReflectiveCallable.run(ReflectiveCallable.java:12)
	at org.junit.runners.model.FrameworkMethod.invokeExplosively(FrameworkMethod.java:56)
	at org.junit.internal.runners.statements.InvokeMethod.evaluate(InvokeMethod.java:17)
	at org.junit.rules.ExternalResource$1.evaluate(ExternalResource.java:54)
	at org.junit.runners.ParentRunner$3.evaluate(ParentRunner.java:306)
	at org.junit.runners.BlockJUnit4ClassRunner$1.evaluate(BlockJUnit4ClassRunner.
```
- FAIL de.ratio.backup.ScannerTest.scan_onlySelectedRoots
```
java.lang.AssertionError: expected:<[DCIM/a.jpg]> but was:<[]>
java.lang.AssertionError: expected:<[DCIM/a.jpg]> but was:<[]>
	at org.junit.Assert.fail(Assert.java:89)
	at org.junit.Assert.failNotEquals(Assert.java:835)
	at org.junit.Assert.assertEquals(Assert.java:120)
	at org.junit.Assert.assertEquals(Assert.java:146)
	at de.ratio.backup.ScannerTest.scan_onlySelectedRoots(ScannerTest.kt:98)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:77)
	at java.base/jdk.internal.reflect.DelegatingMethodAccessorImpl.invoke(DelegatingMethodAccessorImpl.java:43)
	at java.base/java.lang.reflect.Method.invoke(Method.java:569)
	at org.junit.runners.model.FrameworkMethod$1.runReflectiveCall(FrameworkMethod.java:59)
	at org.junit.internal.runners.model.ReflectiveCallable.run(ReflectiveCallable.java:12)
	at org.junit.runners.model.FrameworkMethod.invokeExplosively(FrameworkMethod.java:56)
	at org.junit.internal.runners.statements.InvokeMethod.evaluate(InvokeMethod.java:17)
	at org.junit.rules.ExternalResource$1.evaluate(ExternalResource.java:54)
	at org.junit.runners.Pa
```
Fehlerzeilen:
```
> Task :app:checkKotlinGradlePluginConfigurationErrors SKIPPED
ScannerTest > scan_findsFilesUnderRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:32
ScannerTest > paths_useForwardSlash_relativeToStorageRoot FAILED
    java.util.NoSuchElementException at ScannerTest.kt:78
ScannerTest > scan_missingRoot_notInRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:65
ScannerTest > scan_emptyExistingRoot_isInRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:72
ScannerTest > scan_onlySelectedRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:98
72 tests completed, 5 failed
> Task :app:testDebugUnitTest FAILED
FAILURE: Build failed with an exception.
* What went wrong:
Execution failed for task ':app:testDebugUnitTest'.
BUILD FAILED in 1m 2s
```
Logende:
```
> Task :app:bundleDebugClassesToCompileJar
> Task :app:processDebugJavaRes
> Task :app:compileDebugUnitTestKotlin
> Task :app:dexBuilderRelease
> Task :app:compileDebugUnitTestJavaWithJavac NO-SOURCE
> Task :app:processDebugUnitTestJavaRes

> Task :app:testDebugUnitTest

ScannerTest > scan_findsFilesUnderRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:32

ScannerTest > paths_useForwardSlash_relativeToStorageRoot FAILED
    java.util.NoSuchElementException at ScannerTest.kt:78

ScannerTest > scan_missingRoot_notInRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:65

ScannerTest > scan_emptyExistingRoot_isInRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:72

ScannerTest > scan_onlySelectedRoots FAILED
    java.lang.AssertionError at ScannerTest.kt:98

72 tests completed, 5 failed

> Task :app:testDebugUnitTest FAILED
gradle/actions: Writing build results to /home/runner/work/_temp/.gradle-actions/build-results/build-1795722716545.json

FAILURE: Build failed with an exception.

* What went wrong:
Execution failed for task ':app:testDebugUnitTest'.
> There were failing tests. See the report at: file:///home/runner/work/ratioBackup/ratioBackup/android/app/build/reports/tests/testDebugUnitTest/index.html

* Try:
> Run with --scan to get full insights.

BUILD FAILED in 1m 2s
67 actionable tasks: 67 executed
```
## pc-test: success
Tests: 92 bestanden, 0 fehlgeschlagen
## pc-exe: success
Tests: 0 bestanden, 0 fehlgeschlagen
