package de.ratio.backup.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import de.ratio.backup.backup.BackupService
import de.ratio.backup.backup.BackupState
import de.ratio.backup.backup.Prefs
import de.ratio.backup.backup.RunHistory
import de.ratio.backup.backup.RunRecord
import de.ratio.backup.backup.UiState
import de.ratio.backup.backup.advice
import de.ratio.backup.backup.formatBytes
import de.ratio.backup.backup.formatDuration
import de.ratio.backup.backup.headline
import de.ratio.backup.net.BackupApi
import de.ratio.backup.net.Discovery
import de.ratio.backup.net.Pairing
import de.ratio.backup.net.PinnedClient
import de.ratio.backup.net.Status
import de.ratio.backup.schedule.AlarmScheduler
import de.ratio.backup.update.UpdateInfo
import de.ratio.backup.update.Updater
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

private val fmt = DateTimeFormatter.ofPattern("EEEE, dd.MM. 'um' HH:mm", Locale.GERMAN)
private val shortFmt = DateTimeFormatter.ofPattern("dd.MM. HH:mm", Locale.GERMAN)

@Composable
fun MainScreen(prefs: Prefs, refresh: Int, onOpenSetup: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val state by BackupState.flow.collectAsState()
    var pcStatus by remember { mutableStateOf<Status?>(null) }
    var pcState by remember { mutableStateOf("PC wird gesucht …") }
    var update by remember { mutableStateOf<UpdateInfo?>(null) }
    var updatePercent by remember { mutableStateOf<Int?>(null) }
    var updateError by remember { mutableStateOf<String?>(null) }
    val finished = (state as? UiState.Finished)?.record

    LaunchedEffect(refresh) { update = Updater.check(context) }

    LaunchedEffect(refresh, finished) {
        val token = prefs.token
        val fp = prefs.fingerprint
        if (token == null || fp == null || BackupState.isRunning) return@LaunchedEffect
        val pc = Discovery(context).find()
        if (pc == null || !pc.fingerprint.equals(fp, ignoreCase = true)) {
            pcStatus = null
            pcState = "PC nicht erreichbar (aus oder anderes WLAN)"
            return@LaunchedEffect
        }
        pcStatus = try {
            BackupApi(PinnedClient.create(fp), Pairing.baseUrl(pc), token).status()
        } catch (e: Exception) {
            pcState = "PC antwortet nicht: ${e.message}"
            null
        }
    }

    val history = remember(refresh, finished) { RunHistory.parse(prefs.historyJson) }
    val zone = ZoneId.systemDefault()

    Column(
        Modifier.verticalScroll(rememberScrollState()).padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("RatioBackup", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)

        update?.let { u ->
            Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Update verfügbar · Version ${u.versionName}", style = MaterialTheme.typography.titleMedium)
                    if (u.notes.isNotBlank()) Text(u.notes, style = MaterialTheme.typography.bodySmall)
                    val p = updatePercent
                    if (p != null) LinearProgressIndicator(progress = { p / 100f }, modifier = Modifier.fillMaxWidth())
                    updateError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                    Button(enabled = p == null && !BackupState.isRunning, onClick = {
                        if (!Updater.canInstall(context)) { Updater.openInstallPermission(context); return@Button }
                        updateError = null
                        updatePercent = 0
                        scope.launch {
                            try {
                                Updater.downloadAndInstall(context, u) { updatePercent = it }
                            } catch (e: Exception) {
                                updateError = e.message ?: "Update fehlgeschlagen"
                            }
                            updatePercent = null
                        }
                    }) { Text(if (Updater.canInstall(context)) "Installieren" else "Installieren erlauben") }
                }
            }
        }

        // Live-Status
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(20.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
                when (val s = state) {
                    UiState.Searching, UiState.Comparing -> {
                        CircularProgressIndicator(Modifier.size(120.dp), strokeWidth = 10.dp)
                        Text(if (s == UiState.Searching) "Suche PC …" else "Gleiche mit dem PC ab …", style = MaterialTheme.typography.titleMedium)
                        OutlinedButton(onClick = { BackupService.cancel(context) }) { Text("Abbrechen") }
                    }
                    is UiState.Uploading -> {
                        val p = s.progress
                        Box(contentAlignment = Alignment.Center) {
                            CircularProgressIndicator(progress = { p.percent / 100f }, modifier = Modifier.size(140.dp), strokeWidth = 12.dp)
                            Text("${p.percent} %", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold)
                        }
                        Text("${p.doneFiles} von ${p.totalFiles} Dateien", style = MaterialTheme.typography.titleMedium)
                        Text("${formatBytes(p.doneBytes)} von ${formatBytes(p.totalBytes)} · ${formatBytes(p.bytesPerSec)}/s")
                        p.etaSec?.let { Text("noch ca. ${formatDuration(it)}") }
                        p.currentFile?.let { Text(it.substringAfterLast('/'), style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                        OutlinedButton(onClick = { BackupService.cancel(context) }) { Text("Abbrechen") }
                    }
                    else -> {
                        val last = finished ?: history.firstOrNull()
                        if (last != null) LastRun(last) else Text("Noch keine Sicherung", style = MaterialTheme.typography.titleMedium)
                        Button(modifier = Modifier.fillMaxWidth(), onClick = { BackupService.start(context) }) { Text("Jetzt sichern") }
                    }
                }
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("Nächste automatische Sicherung", style = MaterialTheme.typography.labelLarge)
                Text(remember(refresh, finished) { AlarmScheduler.next(context).format(fmt) }, style = MaterialTheme.typography.titleMedium)
                Text("Letzte erfolgreiche", style = MaterialTheme.typography.labelLarge)
                Text(prefs.lastSuccess?.atZone(zone)?.format(fmt) ?: "noch keine", style = MaterialTheme.typography.titleMedium)
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("PC-Speicher (${prefs.pcName ?: "PC"})", style = MaterialTheme.typography.labelLarge)
                val s = pcStatus
                if (s == null) Text(pcState)
                else {
                    Text("${formatBytes(s.usedBytes)} von ${formatBytes(s.quotaBytes)} belegt", style = MaterialTheme.typography.titleMedium)
                    LinearProgressIndicator(progress = { if (s.quotaBytes > 0) (s.usedBytes.toFloat() / s.quotaBytes).coerceIn(0f, 1f) else 0f }, modifier = Modifier.fillMaxWidth())
                    if (s.pendingDeletionReview) Text(
                        "Am Handy fehlen plötzlich viele Dateien. Zur Sicherheit wurde nichts gelöscht – bitte am PC prüfen.",
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }

        if (!Permissions.ignoresBatteryOptimization(context) || !prefs.autostartConfirmed || !Permissions.hasAllFiles()) {
            Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Nicht alle Freigaben sind erteilt – die automatische Sicherung kann ausfallen.")
                    OutlinedButton(onClick = onOpenSetup) { Text("Einrichtung öffnen") }
                }
            }
        }

        if (history.isNotEmpty()) {
            Text("Verlauf", style = MaterialTheme.typography.titleMedium)
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    history.take(10).forEach { r ->
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                            Column(Modifier.weight(1f)) {
                                Text((if (r.result == "ok") "✓ " else "✗ ") + r.headline(), fontWeight = FontWeight.Medium)
                                Text(r.advice(), style = MaterialTheme.typography.bodySmall)
                            }
                            Text(Instant.ofEpochMilli(r.startMs).atZone(zone).format(shortFmt), style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }

        OutlinedButton(modifier = Modifier.fillMaxWidth(), onClick = onOpenSetup) { Text("Einrichtung") }
        Text("Version ${Updater.currentVersionName}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.outline)
    }
}

@Composable
private fun LastRun(r: RunRecord) {
    val ok = r.result == "ok"
    Text(if (ok) "✓" else "!", style = MaterialTheme.typography.displaySmall,
        color = if (ok) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error)
    Text(r.headline(), style = MaterialTheme.typography.titleLarge)
    Text(r.advice(), style = MaterialTheme.typography.bodyMedium)
}
