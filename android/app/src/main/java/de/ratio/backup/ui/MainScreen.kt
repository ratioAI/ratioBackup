package de.ratio.backup.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import de.ratio.backup.backup.BackupService
import de.ratio.backup.backup.Prefs
import de.ratio.backup.net.BackupApi
import de.ratio.backup.net.Discovery
import de.ratio.backup.net.Pairing
import de.ratio.backup.net.PinnedClient
import de.ratio.backup.net.Status
import de.ratio.backup.schedule.AlarmScheduler
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

private val fmt = DateTimeFormatter.ofPattern("EEEE, dd.MM. 'um' HH:mm", Locale.GERMAN)

@Composable
fun MainScreen(prefs: Prefs, refresh: Int, onOpenSetup: () -> Unit) {
    val context = LocalContext.current
    var pcStatus by remember { mutableStateOf<Status?>(null) }
    var pcState by remember { mutableStateOf("PC wird gesucht …") }

    LaunchedEffect(refresh) {
        val token = prefs.token
        val fp = prefs.fingerprint
        if (token == null || fp == null) return@LaunchedEffect
        val pc = Discovery(context).find()
        pcStatus = null
        if (pc == null || !pc.fingerprint.equals(fp, ignoreCase = true)) {
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

    val zone = ZoneId.systemDefault()
    val last = prefs.lastSuccess?.atZone(zone)?.format(fmt) ?: "noch keine"
    val next = remember(refresh) { AlarmScheduler.next(context).format(fmt) }

    Column(
        Modifier.verticalScroll(rememberScrollState()).padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("RatioBackup", style = MaterialTheme.typography.headlineSmall)
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("Letzte Sicherung", style = MaterialTheme.typography.labelLarge)
                Text(last, style = MaterialTheme.typography.titleMedium)
                prefs.lastStatus?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                Text("Nächste Sicherung", style = MaterialTheme.typography.labelLarge)
                Text(next, style = MaterialTheme.typography.titleMedium)
            }
        }
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("PC-Speicher (${prefs.pcName ?: "PC"})", style = MaterialTheme.typography.labelLarge)
                val s = pcStatus
                if (s == null) Text(pcState)
                else Text("%.1f von %.0f GB belegt".format(Locale.GERMAN, s.usedBytes / GIB, s.quotaBytes / GIB), style = MaterialTheme.typography.titleMedium)
            }
        }
        if (pcStatus?.pendingDeletionReview == true) {
            Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)) {
                Text(
                    "Am Handy fehlen plötzlich viele Dateien. Zur Sicherheit wurde am PC nichts gelöscht – bitte am PC bestätigen.",
                    Modifier.padding(16.dp),
                )
            }
        }
        if (!Permissions.ignoresBatteryOptimization(context) || !prefs.autostartConfirmed || !Permissions.hasAllFiles()) {
            Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Nicht alle Freigaben sind erteilt – die Sicherung kann ausfallen.")
                    OutlinedButton(onClick = onOpenSetup) { Text("Einrichtung öffnen") }
                }
            }
        }
        Button(modifier = Modifier.fillMaxWidth(), onClick = { BackupService.start(context) }) { Text("Jetzt sichern") }
        OutlinedButton(modifier = Modifier.fillMaxWidth(), onClick = onOpenSetup) { Text("Einrichtung") }
    }
}

private const val GIB = 1024.0 * 1024 * 1024
