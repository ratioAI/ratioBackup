package de.ratio.backup.update

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

/** Inhalt von version.json im Release "test". */
@Serializable
data class UpdateInfo(
    val versionCode: Int,
    val versionName: String,
    val notes: String = "",
    val apk: String = "RatioBackup.apk",
    val exe: String = "RatioBackup-PC.exe",
) {
    fun isNewerThan(code: Int): Boolean = versionCode > code
    val apkUrl: String get() = BASE + apk

    companion object {
        const val BASE = "https://github.com/ratioAI/ratioBackup/releases/download/test/"
        const val VERSION_URL = BASE + "version.json"
        private val json = Json { ignoreUnknownKeys = true }
        fun parse(text: String): UpdateInfo? = runCatching { json.decodeFromString(serializer(), text) }.getOrNull()
        fun serializerJson(info: UpdateInfo): String = json.encodeToString(serializer(), info)
    }
}
