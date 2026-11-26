package de.ratio.backup.ui

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import de.ratio.backup.backup.Prefs
import de.ratio.backup.net.Discovery
import de.ratio.backup.net.Pairing
import de.ratio.backup.net.PcInfo
import kotlinx.coroutines.launch

const val DEVICE_NAME = "Xiaomi 18 Pro"

@Composable
fun SetupScreen(prefs: Prefs, refresh: Int, onDone: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var notifGranted by remember { mutableStateOf(Permissions.hasNotifications(context)) }
    val notifLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { notifGranted = it }
    var autostart by remember { mutableStateOf(prefs.autostartConfirmed) }
    var pc by remember { mutableStateOf<PcInfo?>(null) }
    var code by remember { mutableStateOf("") }
    var message by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    var paired by remember { mutableStateOf(prefs.isPaired) }

    // Nach Rückkehr aus den Systemeinstellungen neu prüfen.
    val allFiles = remember(refresh) { Permissions.hasAllFiles() }
    val battery = remember(refresh) { Permissions.ignoresBatteryOptimization(context) }
    notifGranted = remember(refresh) { Permissions.hasNotifications(context) } || notifGranted

    Column(
        Modifier.verticalScroll(rememberScrollState()).padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("RatioBackup einrichten", style = MaterialTheme.typography.headlineSmall)
        Text("Damit die Sicherung jeden Abend ab 20 Uhr von allein läuft, braucht die App ein paar Freigaben.")

        Step(1, "Zugriff auf alle Dateien", "Nötig, um Fotos, Videos, Downloads und WhatsApp lesen zu können.", allFiles) {
            Permissions.openAllFiles(context)
        }
        Step(2, "Benachrichtigungen", "Für den Fortschritt und Warnungen (z. B. PC-Speicher voll).", notifGranted) {
            notifLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
        Step(3, "Akku: keine Einschränkungen", "Sonst beendet HyperOS die App und die Sicherung fällt aus.", battery) {
            Permissions.openBattery(context)
        }
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("${if (autostart) "✅" else "4."} Autostart erlauben", style = MaterialTheme.typography.titleMedium)
                Text("In der Xiaomi-Autostart-Liste RatioBackup einschalten. Das kann die App nicht selbst prüfen.")
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(onClick = { Permissions.openAutostart(context) }) { Text("Öffnen") }
                    Button(onClick = { prefs.autostartConfirmed = true; autostart = true }) { Text("Erledigt") }
                }
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("${if (paired) "✅" else "5."} Mit dem PC koppeln", style = MaterialTheme.typography.titleMedium)
                if (paired) {
                    Text("Gekoppelt mit ${prefs.pcName ?: "PC"}.")
                } else {
                    Text("Starte RatioBackup am PC. Handy und PC müssen im selben WLAN sein.")
                    if (pc == null) {
                        Button(enabled = !busy, onClick = {
                            busy = true; message = null
                            scope.launch {
                                pc = Discovery(context).find()
                                if (pc == null) message = "Kein PC gefunden. Läuft RatioBackup am PC? Gleiches WLAN?"
                                busy = false
                            }
                        }) { Text(if (busy) "Suche …" else "PC suchen") }
                    } else {
                        Text("Gefunden: ${pc!!.name}")
                        Text(
                            "Prüfcode ${de.ratio.backup.net.verifyCode(pc!!.fingerprint)} – der PC muss denselben zeigen. Stimmt er nicht, nicht koppeln.",
                            style = MaterialTheme.typography.bodySmall,
                        )
                        Text("Gib den 6-stelligen Code ein, den der PC anzeigt:")
                        OutlinedTextField(
                            value = code,
                            onValueChange = { v -> code = v.filter(Char::isDigit).take(6) },
                            label = { Text("6-stelliger Code") },
                            singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
                        )
                        Button(enabled = code.length == 6 && !busy, onClick = {
                            busy = true; message = null
                            val target = pc!!
                            scope.launch {
                                try {
                                    prefs.token = Pairing.pair(target, code, DEVICE_NAME)
                                    prefs.fingerprint = target.fingerprint
                                    prefs.pcName = target.name
                                    paired = true
                                } catch (e: Exception) {
                                    message = e.message ?: "Koppeln fehlgeschlagen"
                                }
                                busy = false
                            }
                        }) { Text("Koppeln") }
                    }
                }
                message?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        }

        Spacer(Modifier.width(1.dp))
        Button(
            modifier = Modifier.fillMaxWidth(),
            enabled = allFiles && paired,
            onClick = onDone,
        ) { Text("Fertig") }
        if (!(allFiles && paired)) Text("„Fertig“ geht, sobald Schritt 1 und 5 erledigt sind.", style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun Step(n: Int, title: String, text: String, done: Boolean, onClick: () -> Unit) {
    Card(Modifier.fillMaxWidth()) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text("${if (done) "✅" else "$n."} $title", style = MaterialTheme.typography.titleMedium)
                Text(text, style = MaterialTheme.typography.bodyMedium)
            }
            if (!done) OutlinedButton(onClick = onClick) { Text("Öffnen") }
        }
    }
}
