package de.ratio.backup.ui

import android.app.TimePickerDialog
import android.os.Environment
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import de.ratio.backup.backup.Prefs
import de.ratio.backup.backup.formatBytes
import de.ratio.backup.net.verifyCode
import de.ratio.backup.scan.Category
import de.ratio.backup.schedule.AlarmScheduler
import de.ratio.backup.update.Updater
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.time.LocalTime

@Composable
fun SettingsScreen(prefs: Prefs, onOpenSetup: () -> Unit, onRepair: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var start by remember { mutableStateOf(prefs.startTime) }
    var cats by remember { mutableStateOf(prefs.categories) }
    val sizes = remember { mutableStateMapOf<Category, Long>() }
    var updateText by remember { mutableStateOf("Version ${Updater.currentVersionName}") }

    LaunchedEffect(Unit) {
        val storage = Environment.getExternalStorageDirectory()
        withContext(Dispatchers.IO) {
            Category.entries.forEach { c ->
                val bytes = c.roots.sumOf { r -> File(storage, r).walkTopDown().filter { it.isFile }.sumOf { it.length() } }
                sizes[c] = bytes
            }
        }
    }

    Column(Modifier.verticalScroll(rememberScrollState())) {
        ScreenTitle("RatioBackup ${Updater.currentVersionName}", "Einstellungen")
        Column(Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp, bottom = 16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Group("Zeitplan") {
                Item("Tägliche Sicherung", "Bei Fehlschlag alle 5 Min bis +30 Min", valueText = "%02d:%02d".format(start.hour, start.minute)) {
                    TimePickerDialog(context, { _, h, m ->
                        val t = LocalTime.of(h, m)
                        prefs.startTime = t
                        start = t
                        AlarmScheduler.scheduleNext(context)
                    }, start.hour, start.minute, true).show()
                }
            }
            Group("Was wird gesichert") {
                Category.entries.forEachIndexed { i, c ->
                    if (i > 0) Divider()
                    val on = c in cats
                    Row(
                        Modifier.fillMaxWidth().heightIn(min = 56.dp).clickable {
                            cats = if (on) cats - c else cats + c
                            prefs.categories = cats
                        }.padding(horizontal = 16.dp, vertical = 10.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            Text(c.label, fontSize = 15.sp, fontWeight = FontWeight.Medium)
                            Text(
                                c.roots.joinToString(" · ") { it.substringAfterLast('/') } + (sizes[c]?.let { " · ${formatBytes(it)}" } ?: ""),
                                fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        Switch(checked = on, onCheckedChange = { v ->
                            cats = if (v) cats + c else cats - c
                            prefs.categories = cats
                        })
                    }
                }
            }
            Group("PC") {
                Item(
                    "Gekoppelt mit ${prefs.pcName ?: "–"}",
                    prefs.fingerprint?.let { "Prüfcode ${verifyCode(it)}" } ?: "Noch nicht gekoppelt",
                    valueText = "Neu koppeln", onClick = onRepair,
                )
                Divider()
                Item("Freigaben prüfen", "Dateien, Benachrichtigungen, Akku, Autostart", valueText = "", onClick = onOpenSetup)
            }
            Group("App") {
                Item("Updates", updateText, valueText = "Prüfen") {
                    scope.launch {
                        updateText = "Suche …"
                        val u = Updater.check(context, force = true)
                        updateText = if (u != null) "Version ${u.versionName} verfügbar – siehe Übersicht" else "Version ${Updater.currentVersionName} · aktuell"
                    }
                }
            }
        }
    }
}

@Composable
private fun Group(title: String, content: @Composable () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(title.uppercase(), fontSize = 12.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant,
            letterSpacing = 0.8.sp, modifier = Modifier.padding(start = 4.dp))
        RCard(radius = 18.dp, padding = 0.dp) { Column { content() } }
    }
}

@Composable
private fun Divider() = HorizontalDivider(Modifier.padding(start = 16.dp), color = MaterialTheme.colorScheme.outlineVariant)

@Composable
private fun Item(title: String, sub: String, valueText: String, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 56.dp).clickable(onClick = onClick).padding(horizontal = 16.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(title, fontSize = 15.sp, fontWeight = FontWeight.Medium)
            Text(sub, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Text(if (valueText.isEmpty()) "›" else "$valueText ›", fontSize = 14.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.primary)
    }
}
