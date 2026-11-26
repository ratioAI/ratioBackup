package de.ratio.backup.backup

import de.ratio.backup.net.BackupApiPort
import de.ratio.backup.net.FileChangedException
import de.ratio.backup.net.PcErrorException
import de.ratio.backup.net.UploadResult
import de.ratio.backup.scan.FileEntry
import de.ratio.backup.scan.ScanResult
import kotlinx.coroutines.CancellationException
import java.io.File
import java.io.FileNotFoundException

sealed interface RunOutcome {
    data class Success(val uploaded: Int, val skipped: Int) : RunOutcome
    data object NotPaired : RunOutcome
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
) {
    suspend fun run(onProgress: (done: Int, total: Int) -> Unit): RunOutcome {
        // Ohne Dateizugriff sähe der PC leere Ordner und würde Sicherungen in den Papierkorb legen.
        if (!hasStorageAccess()) return RunOutcome.NoStorageAccess
        try {
            val result = scan()
            val byPath = result.files.associateBy { it.path }
            val manifest = api.manifest(result.roots, result.files)
            val todo = manifest.needed.mapNotNull { n -> byPath[n.path]?.let { it to n.offset } }
            var uploaded = 0
            var skipped = 0
            onProgress(0, todo.size)
            for ((i, item) in todo.withIndex()) {
                when (uploadOne(item.first, item.second)) {
                    Step.Done -> uploaded++
                    Step.Skipped -> skipped++
                    Step.QuotaFull -> return RunOutcome.QuotaFull
                }
                onProgress(i + 1, todo.size)
            }
            api.complete()
            return RunOutcome.Success(uploaded, skipped)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            return RunOutcome.Failed(e.message ?: e.javaClass.simpleName)
        }
    }

    private enum class Step { Done, Skipped, QuotaFull }

    private suspend fun uploadOne(entry: FileEntry, startOffset: Long): Step {
        var offset = startOffset
        repeat(MAX_ATTEMPTS) {
            val r = try {
                api.upload(open(entry), entry, offset)
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
    }
}
