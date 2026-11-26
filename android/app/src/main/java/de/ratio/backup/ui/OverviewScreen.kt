package de.ratio.backup.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.DateRange
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
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
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

private val timeFmt = DateTimeFormatter.ofPattern("HH:mm", Locale.GERMAN)
private val dayFmt = DateTimeFormatter.ofPattern("EEEE, dd.MM.", Locale.GERMAN)

/** „Heute, 20:04" / „Morgen, 20:00" / „Montag, 30.11., 20:00". */
fun friendly(t: ZonedDateTime): String {
    val today = LocalDate.now(t.zone)
    val day = when (t.toLocalDate()) {
        today -> "Heute"
        today.plusDays(1) -> "Morgen"
        today.minusDays(1) -> "Gestern"
        else -> t.format(dayFmt)
    }
    return "$day, ${t.format(timeFmt)}"
}

@Composable
fun OverviewScreen(prefs: Prefs, refresh: Int, onOpenSetup: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val state by BackupState.flow.collectAsState()
    var pcStatus by remember { mutableStateOf<Status?>(null) }
    var pcReachable by remember { mutableStateOf<Boolean?>(null) }
    var update by remember { mutableStateOf<UpdateInfo?>(null) }
    var updatePercent by remember { mutableStateOf<Int?>(null) }
    var updateError by remember { mutableStateOf<String?>(null) }
    val finished = (state as? UiState.Finished)?.record
    val running = state is UiState.Searching || state is UiState.Comparing || state is UiState.Uploading

    LaunchedEffect(refresh) { update = Updater.check(context) }
    LaunchedEffect(refresh, finished) {
        val token = prefs.token
        val fp = prefs.fingerprint
        if (token == null || fp == null || BackupState.isRunning) return@LaunchedEffect
        val pc = Discovery(context).find()
        if (pc == null || !pc.fingerprint.equals(fp, ignoreCase = true)) { pcReachable = false; return@LaunchedEffect }
        pcStatus = runCatching { BackupApi(PinnedClient.create(fp), Pairing.baseUrl(pc), token).status() }.getOrNull()
        pcReachable = pcStatus != null
    }
    val history = remember(refresh, finished) { RunHistory.parse(prefs.historyJson) }
    val zone = ZoneId.systemDefault()
    val x = LocalExtra.current

    Column(Modifier.verticalScroll(rememberScrollState())) {
        ScreenTitle("${DEVICE_NAME} → ${prefs.pcName ?: "PC"}", "RatioBackup") {
            when {
                running -> StatusPill("PC verbunden", true)
                pcReachable == true -> StatusPill("PC verbunden", true)
                pcReachable == false -> StatusPill("PC nicht erreichbar", false)
            }
        }
        Column(Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp, bottom = 16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            update?.let { u ->
                RCard(color = MaterialTheme.colorScheme.primaryContainer, padding = 16.dp) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            Text("Update verfügbar · ${u.versionName}", fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                            if (u.notes.isNotBlank()) Text(u.notes, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        }
                        Button(
                            enabled = updatePercent == null && !running,
                            onClick = {
                                if (!Updater.canInstall(context)) { Updater.openInstallPermission(context); return@Button }
                                updateError = null; updatePercent = 0
                                scope.launch {
                                    try { Updater.downloadAndInstall(context, u) { updatePercent = it } }
                                    catch (e: Exception) { updateError = e.message ?: "Update fehlgeschlagen" }
                                    updatePercent = null
                                }
                            },
                            shape = RoundedCornerShape(10.dp),
                        ) { Text(if (Updater.canInstall(context)) "Installieren" else "Erlauben", fontWeight = FontWeight.Bold) }
                    }
                    updatePercent?.let { LinearProgressIndicator(progress = { it / 100f }, modifier = Modifier.fillMaxWidth()) }
                    updateError?.let { Text(it, color = MaterialTheme.colorScheme.error, fontSize = 12.sp) }
                }
            }

            RCard(radius = 24.dp, padding = 20.dp) {
                Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(14.dp)) {
                    when (val s = state) {
                        UiState.Searching, UiState.Comparing -> {
                            Kicker("Sicherung startet")
                            Box(Modifier.size(196.dp), contentAlignment = Alignment.Center) {
                                CircularProgressIndicator(Modifier.size(196.dp), strokeWidth = 16.dp, trackColor = x.track)
                                Text(if (s == UiState.Searching) "Suche PC …" else "Gleiche ab …", fontWeight = FontWeight.SemiBold)
                            }
                            CancelButton { BackupService.cancel(context) }
                        }
                        is UiState.Uploading -> {
                            val p = s.progress
                            Kicker("Sicherung läuft")
                            ProgressRing(p.percent) {
                                Text("${p.percent} %", fontSize = 48.sp, fontWeight = FontWeight.Bold, letterSpacing = (-1).sp)
                                Text(p.etaSec?.let { "noch ca. ${formatDuration(it)}" } ?: "berechne …", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                StatTile("${p.doneFiles} / ${p.totalFiles}", "Dateien", Modifier.weight(1f))
                                StatTile("${formatBytes(p.doneBytes)}", "von ${formatBytes(p.totalBytes)}", Modifier.weight(1f))
                                StatTile("${formatBytes(p.bytesPerSec)}/s", "Tempo", Modifier.weight(1f))
                            }
                            p.currentFile?.let { Text(it.substringAfterLast('/'), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                            CancelButton { BackupService.cancel(context) }
                        }
                        else -> {
                            val last = finished ?: history.firstOrNull()
                            LastRunBlock(last, zone)
                            Button(
                                onClick = { BackupService.start(context) },
                                modifier = Modifier.fillMaxWidth().height(52.dp),
                                shape = RoundedCornerShape(14.dp),
                            ) { Text("Jetzt sichern", fontSize = 16.sp, fontWeight = FontWeight.Bold) }
                        }
                    }
                }
            }

            pcStatus?.let { s ->
                val mirror = (s.usedBytes - s.trashBytes).coerceAtLeast(0)
                RCard {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text("PC-Speicher", fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                        Text("${formatBytes(s.usedBytes)} von ${formatBytes(s.quotaBytes)}", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    StorageBar(mirror, s.trashBytes, s.quotaBytes)
                    Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                        Legend(MaterialTheme.colorScheme.primary, "Sicherung ${formatBytes(mirror)}")
                        Legend(x.warn, "Papierkorb ${formatBytes(s.trashBytes)}")
                    }
                    if (s.pendingDeletionReview) Text(
                        "Am Handy fehlen plötzlich viele Dateien. Zur Sicherheit wurde nichts gelöscht – bitte am PC prüfen.",
                        color = MaterialTheme.colorScheme.error, fontSize = 13.sp,
                    )
                }
            }

            RCard {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                    IconBadge(MaterialTheme.colorScheme.primaryContainer) {
                        Icon(Icons.Filled.DateRange, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(22.dp))
                    }
                    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                        Text("Nächste automatische Sicherung", fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                        Text("${friendly(remember(refresh, finished) { AlarmScheduler.next(context) })} · nur im WLAN", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }

            if (!Permissions.ignoresBatteryOptimization(context) || !prefs.autostartConfirmed || !Permissions.hasAllFiles()) {
                RCard(color = x.warnBg) {
                    Text("Nicht alle Freigaben sind erteilt – die automatische Sicherung kann ausfallen.", fontSize = 13.sp)
                    OutlinedButton(onClick = onOpenSetup, shape = RoundedCornerShape(12.dp)) { Text("Freigaben prüfen") }
                }
            }
        }
    }
}

@Composable
private fun Kicker(text: String) =
    Text(text.uppercase(), fontSize = 13.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.primary, letterSpacing = 0.8.sp)

@Composable
private fun CancelButton(onClick: () -> Unit) =
    OutlinedButton(onClick = onClick, modifier = Modifier.fillMaxWidth().height(48.dp), shape = RoundedCornerShape(14.dp)) {
        Text("Abbrechen", fontWeight = FontWeight.SemiBold)
    }

@Composable
private fun LastRunBlock(last: RunRecord?, zone: ZoneId) {
    val x = LocalExtra.current
    val ok = last == null || last.result == "ok"
    Box(
        Modifier.size(112.dp).clip(CircleShape).background(if (ok) x.successBg else x.warnBg),
        contentAlignment = Alignment.Center,
    ) {
        Icon(if (ok) Icons.Filled.Check else Icons.Filled.Warning, null, tint = if (ok) x.success else x.warn, modifier = Modifier.size(56.dp))
    }
    if (last == null) {
        Text("Noch keine Sicherung", fontSize = 22.sp, fontWeight = FontWeight.Bold)
        Text("Tipp auf „Jetzt sichern“ oder warte bis zur geplanten Zeit.", fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center)
    } else {
        Text(if (last.result == "ok") "Alles gesichert" else last.headline(), fontSize = 22.sp, fontWeight = FontWeight.Bold)
        val whenText = friendly(Instant.ofEpochMilli(last.endMs).atZone(zone))
        val dur = formatDuration((last.endMs - last.startMs) / 1000)
        Text(
            if (last.result == "ok") "$whenText · ${last.advice()} · $dur" else "$whenText · ${last.advice()}",
            fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center,
        )
    }
    Spacer(Modifier.height(2.dp))
}
