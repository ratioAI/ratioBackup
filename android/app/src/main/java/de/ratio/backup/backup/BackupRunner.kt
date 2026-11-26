package de.ratio.backup.backup

import de.ratio.backup.net.BackupApiPort
import de.ratio.backup.net.FileChangedException
import de.ratio.backup.net.PcErrorException
import de.ratio.backup.net.UploadResult
import de.ratio.backup.scan.FileEntry
import de.ratio.backup.scan.ScanResult
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.isActive
import java.io.File
import java.io.FileNotFoundException

sealed interface RunOutcome {
    data class Success(val uploaded: Int, val skipped: Int) : RunOutcome
    data object NotPaired : RunOutcome
    data object Cancelled : RunOutcome
    data object NoStorageAccess : RunOutcome
    data object PcNotFound : RunOutcome
    data object QuotaFull : RunOutcome
    data class Failed(val reason: String) : RunOutcome
}

/** Ein Sicherungslauf: abgleichen, fehlende Dateien hochladen, abschließen. */
class BackupRunner(
    private val scan: () -> ScanResult,
    private val api: BackupApiPort,
    private val open: (FileEntry) -> File,
    private val hasStorageAccess: () -> Boolean = { true },
    private val nowMs: () -> Long = System::currentTimeMillis,
) {
    private var lastEmit = 0L

    suspend fun run(onProgress: (Progress) -> Unit): RunOutcome {
        // Ohne Dateizugriff sähe der PC leere Ordner und würde Sicherungen in den Papierkorb legen.
        if (!hasStorageAccess()) return RunOutcome.NoStorageAccess
        try {
            val result = scan()
            val byPath = result.files.associateBy { it.path }
            val manifest = api.manifest(result.roots, result.files)
            val todo = manifest.needed.mapNotNull { n -> byPath[n.path]?.let { it to n.offset } }
            val t = ProgressTracker(todo.size, todo.sumOf { (e, o) -> (e.size - o).coerceAtLeast(0) }, nowMs)
            var uploaded = 0
            var skipped = 0
            onProgress(t.snapshot())
            for ((entry, offset) in todo) {
                t.fileStarted(entry.path)
                onProgress(t.snapshot())
                val before = t.snapshot().doneBytes
                when (uploadOne(entry, offset) { n -> t.addBytes(n); emit(t, onProgress) }) {
                    Step.Done -> uploaded++
                    Step.Skipped -> {
                        skipped++
                        t.dropBytes((entry.size - offset) - (t.snapshot().doneBytes - before))
                    }
                    Step.QuotaFull -> return RunOutcome.QuotaFull
                }
                t.fileDone()
                onProgress(t.snapshot())
            }
            api.complete()
            return RunOutcome.Success(uploaded, skipped)
        } catch (e: CancellationException) {
            return RunOutcome.Cancelled
        } catch (e: Exception) {
            // Ein abgebrochener Upload wirft eine IOException – trotzdem als „Abgebrochen" melden.
            if (!currentCoroutineContext().isActive) return RunOutcome.Cancelled
            return RunOutcome.Failed(e.message ?: e.javaClass.simpleName)
        }
    }

    private fun emit(t: ProgressTracker, onProgress: (Progress) -> Unit) {
        val now = nowMs()
        if (now - lastEmit >= EMIT_MS) {
            lastEmit = now
            onProgress(t.snapshot())
        }
    }

    private enum class Step { Done, Skipped, QuotaFull }

    private suspend fun uploadOne(entry: FileEntry, startOffset: Long, onBytes: (Long) -> Unit): Step {
        var offset = startOffset
        repeat(MAX_ATTEMPTS) {
            val r = try {
                api.upload(open(entry), entry, offset, onBytes)
            } catch (e: PcErrorException) {
                // Token ungültig: alle weiteren Dateien würden auch scheitern – Lauf abbrechen.
                if (e.code == 401 || e.code == 403) throw e
                return Step.Skipped
            } catch (e: FileNotFoundException) {
                return Step.Skipped
            } catch (e: FileChangedException) {
                return Step.Skipped
            }
            when (r) {
                UploadResult.Done -> return Step.Done
                UploadResult.QuotaFull -> return Step.QuotaFull
                // Datei hat sich während des Hochladens geändert: beim nächsten Lauf neu.
                UploadResult.Corrupt -> return Step.Skipped
                is UploadResult.WrongOffset -> offset = r.offset
                is UploadResult.Partial -> offset = r.offset
            }
        }
        return Step.Skipped
    }

    companion object {
        private const val MAX_ATTEMPTS = 2
        private const val EMIT_MS = 500L
    }
}
