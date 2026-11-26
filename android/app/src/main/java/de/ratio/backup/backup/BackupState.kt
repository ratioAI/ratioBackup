package de.ratio.backup.backup

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

sealed interface UiState {
    data object Idle : UiState
    data object Searching : UiState
    data object Comparing : UiState
    data class Uploading(val progress: Progress) : UiState
    data class Finished(val record: RunRecord) : UiState
}

/** Zustand des Sicherungsdienstes, den die Oberfläche live beobachtet. */
object BackupState {
    private val state = MutableStateFlow<UiState>(UiState.Idle)
    val flow: StateFlow<UiState> = state.asStateFlow()
    fun set(s: UiState) { state.value = s }
    val isRunning: Boolean get() = state.value.let { it is UiState.Searching || it is UiState.Comparing || it is UiState.Uploading }
}

/** Klartext je Ergebnis – mit dem, was der Nutzer tun kann. */
fun RunRecord.headline(): String = when (result) {
    "ok" -> if (uploadedFiles == 0) "Alles aktuell" else "Erfolgreich gesichert"
    "pc_not_found" -> "PC nicht gefunden"
    "quota" -> "Speicherlimit am PC erreicht"
    "no_access" -> "Kein Zugriff auf die Dateien"
    "cancelled" -> "Abgebrochen"
    "not_paired" -> "Noch nicht gekoppelt"
    else -> "Sicherung abgebrochen"
}

fun RunRecord.advice(): String = when (result) {
    "ok" -> buildString {
        append("$uploadedFiles Dateien · ${formatBytes(bytes)}")
        if (skippedFiles > 0) append(" · $skippedFiles beim nächsten Mal")
    }
    "pc_not_found" -> "Ist RatioBackup am PC gestartet und das Handy im selben WLAN?"
    "quota" -> "Erhöhe das Limit im PC-Programm oder leere den Papierkorb."
    "no_access" -> "Erlaube in der Einrichtung „Zugriff auf alle Dateien“."
    "cancelled" -> "Beim nächsten Mal geht es an derselben Stelle weiter."
    "not_paired" -> "Öffne die Einrichtung und kopple das Handy mit dem PC."
    else -> "$message – beim nächsten Mal geht es an derselben Stelle weiter."
}

fun formatBytes(b: Long): String = when {
    b >= 1L shl 30 -> "%.1f GB".format(java.util.Locale.GERMAN, b / (1L shl 30).toDouble())
    b >= 1L shl 20 -> "%.0f MB".format(java.util.Locale.GERMAN, b / (1L shl 20).toDouble())
    b >= 1L shl 10 -> "%.0f KB".format(java.util.Locale.GERMAN, b / 1024.0)
    else -> "$b B"
}

fun formatDuration(sec: Long): String = when {
    sec >= 3600 -> "${sec / 3600} Std ${(sec % 3600) / 60} Min"
    sec >= 60 -> "${sec / 60} Min"
    else -> "< 1 Min"
}
