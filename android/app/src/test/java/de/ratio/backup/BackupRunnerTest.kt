package de.ratio.backup

import de.ratio.backup.backup.BackupRunner
import de.ratio.backup.backup.RunOutcome
import de.ratio.backup.net.BackupApiPort
import de.ratio.backup.net.ManifestResult
import de.ratio.backup.net.NeededFile
import de.ratio.backup.net.Status
import de.ratio.backup.net.UploadResult
import de.ratio.backup.scan.FileEntry
import de.ratio.backup.scan.ScanResult
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.io.IOException

class BackupRunnerTest {
    private val a = FileEntry("DCIM/a.jpg", 10, 1)
    private val b = FileEntry("DCIM/b.jpg", 20, 1)
    private val scan = ScanResult(listOf("DCIM"), listOf(a, b))

    private class FakeApi(
        val needed: List<NeededFile>,
        val answers: MutableList<UploadResult> = mutableListOf(),
        val failUpload: Boolean = false,
    ) : BackupApiPort {
        val uploads = mutableListOf<Pair<String, Long>>()
        var completed = false
        var manifestRoots: List<String>? = null

        override suspend fun status() = Status(0, 0, 0)
        override suspend fun manifest(roots: List<String>, files: List<FileEntry>): ManifestResult {
            manifestRoots = roots
            return ManifestResult(needed, 0, 0)
        }
        override suspend fun upload(file: File, entry: FileEntry, offset: Long): UploadResult {
            if (failUpload) throw IOException("WLAN weg")
            uploads += entry.path to offset
            return if (answers.isEmpty()) UploadResult.Done else answers.removeAt(0)
        }
        override suspend fun complete() { completed = true }
    }

    private fun runner(api: FakeApi) = BackupRunner({ scan }, api, { File(it.path) })

    @Test
    fun uploadsOnlyNeeded_thenComplete() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/b.jpg", 0)))
        val progress = mutableListOf<Pair<Int, Int>>()
        val r = runner(api).run { d, t -> progress += d to t }
        assertEquals(RunOutcome.Success(uploaded = 1, skipped = 0), r)
        assertEquals(listOf("DCIM/b.jpg" to 0L), api.uploads)
        assertTrue(api.completed)
        assertEquals(listOf("DCIM"), api.manifestRoots)
        assertEquals(1 to 1, progress.last())
    }

    @Test
    fun resumesFromOffsetGivenByManifest() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/b.jpg", 12)))
        runner(api).run { _, _ -> }
        assertEquals(listOf("DCIM/b.jpg" to 12L), api.uploads)
    }

    @Test
    fun wrongOffset_retriesOnceFromServerOffset() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/b.jpg", 0)), mutableListOf(UploadResult.WrongOffset(7), UploadResult.Done))
        val r = runner(api).run { _, _ -> }
        assertEquals(listOf("DCIM/b.jpg" to 0L, "DCIM/b.jpg" to 7L), api.uploads)
        assertEquals(RunOutcome.Success(1, 0), r)
    }

    @Test
    fun repeatedWrongOffset_skipsFile() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/b.jpg", 0)),
            mutableListOf(UploadResult.WrongOffset(7), UploadResult.WrongOffset(9), UploadResult.WrongOffset(9)))
        assertEquals(RunOutcome.Success(0, 1), runner(api).run { _, _ -> })
    }

    @Test
    fun corrupt_skipsFile_runStillCompletes() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/a.jpg", 0), NeededFile("DCIM/b.jpg", 0)), mutableListOf(UploadResult.Corrupt))
        val r = runner(api).run { _, _ -> }
        assertEquals(RunOutcome.Success(uploaded = 1, skipped = 1), r)
        assertTrue(api.completed)
    }

    @Test
    fun quotaFull_stopsWithoutComplete() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/a.jpg", 0), NeededFile("DCIM/b.jpg", 0)), mutableListOf(UploadResult.QuotaFull))
        assertEquals(RunOutcome.QuotaFull, runner(api).run { _, _ -> })
        assertEquals(1, api.uploads.size)
        assertFalse(api.completed)
    }

    @Test
    fun networkError_returnsFailed_noComplete() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/a.jpg", 0)), failUpload = true)
        assertTrue(runner(api).run { _, _ -> } is RunOutcome.Failed)
        assertFalse(api.completed)
    }

    @Test
    fun neededPathUnknownToPhone_ignored() = runBlocking {
        val api = FakeApi(listOf(NeededFile("DCIM/fremd.jpg", 0)))
        assertEquals(RunOutcome.Success(0, 0), runner(api).run { _, _ -> })
        assertTrue(api.uploads.isEmpty())
    }
}
