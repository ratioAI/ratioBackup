package de.ratio.backup.backup

import kotlinx.serialization.Serializable
import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.json.Json

/** Ein abgeschlossener Sicherungslauf für den Verlauf. */
@Serializable
data class RunRecord(
    val startMs: Long,
    val endMs: Long,
    val uploadedFiles: Int,
    val skippedFiles: Int,
    val bytes: Long,
    /** ok | pc_not_found | quota | no_access | cancelled | failed | not_paired */
    val result: String,
    val message: String,
)

object RunHistory {
    const val MAX = 30
    private val json = Json { ignoreUnknownKeys = true }
    private val ser = ListSerializer(RunRecord.serializer())

    /** Neueste zuerst. */
    fun parse(text: String?): List<RunRecord> =
        if (text.isNullOrBlank()) emptyList() else runCatching { json.decodeFromString(ser, text) }.getOrDefault(emptyList())

    fun add(text: String?, r: RunRecord): String = json.encodeToString(ser, (listOf(r) + parse(text)).take(MAX))
}
