package de.ratio.backup.net

import de.ratio.backup.scan.FileEntry
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

internal val json = Json { ignoreUnknownKeys = true; encodeDefaults = true }

@Serializable
data class Status(
    val usedBytes: Long,
    val quotaBytes: Long,
    val trashBytes: Long,
    val lastBackupUtc: String? = null,
    val pendingDeletionReview: Boolean = false,
)

@Serializable data class ManifestRequest(val roots: List<String>, val files: List<FileEntry>)
@Serializable data class NeededFile(val path: String, val offset: Long)
@Serializable data class ManifestResult(val needed: List<NeededFile>, val trashed: Int, val heldBackDeletions: Int)
@Serializable internal data class OffsetDto(val offset: Long)
@Serializable internal data class PairRequest(val code: String, val deviceName: String)
@Serializable internal data class PairResponse(val token: String, val proof: String)

data class PcInfo(val host: String, val port: Int, val name: String, val fingerprint: String)

sealed interface UploadResult {
    data object Done : UploadResult
    data class Partial(val offset: Long) : UploadResult
    data class WrongOffset(val offset: Long) : UploadResult
    data object Corrupt : UploadResult
    data object QuotaFull : UploadResult
}

/** Schnittstelle des Sicherungsablaufs zum PC (für Tests austauschbar). */
interface BackupApiPort {
    suspend fun status(): Status
    suspend fun manifest(roots: List<String>, files: List<FileEntry>): ManifestResult
    suspend fun upload(file: java.io.File, entry: FileEntry, offset: Long): UploadResult
    suspend fun complete()
}
