package de.ratio.backup.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import de.ratio.backup.backup.BackupState
import de.ratio.backup.backup.Prefs
import de.ratio.backup.backup.RunHistory
import de.ratio.backup.backup.UiState
import de.ratio.backup.backup.advice
import de.ratio.backup.backup.formatBytes
import de.ratio.backup.backup.formatDuration
import de.ratio.backup.backup.headline
import java.time.Instant
import java.time.ZoneId

@Composable
fun HistoryScreen(prefs: Prefs, refresh: Int) {
    val state by BackupState.flow.collectAsState()
    val finished = (state as? UiState.Finished)?.record
    val history = remember(refresh, finished) { RunHistory.parse(prefs.historyJson) }
    val ok = history.filter { it.result == "ok" }
    val zone = ZoneId.systemDefault()
    val x = LocalExtra.current

    Column(Modifier.verticalScroll(rememberScrollState())) {
        ScreenTitle("Letzte ${RunHistory.MAX} Läufe", "Verlauf")
        Column(Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp, bottom = 16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Stat("${ok.size} / ${history.size}", "erfolgreich", Modifier.weight(1f))
                Stat(formatBytes(history.sumOf { it.bytes }), "gesichert", Modifier.weight(1f))
                Stat(if (ok.isEmpty()) "–" else formatDuration(ok.map { (it.endMs - it.startMs) / 1000 }.average().toLong()), "Ø Dauer", Modifier.weight(1f))
            }
            if (history.isEmpty()) {
                RCard { Text("Noch keine Läufe. Die erste Sicherung erscheint hier.", color = MaterialTheme.colorScheme.onSurfaceVariant) }
            } else {
                RCard(padding = 0.dp) {
                    history.forEachIndexed { i, r ->
                        if (i > 0) HorizontalDivider(Modifier.padding(start = 66.dp), color = MaterialTheme.colorScheme.outlineVariant)
                        val (icon, fg, bg) = when (r.result) {
                            "ok" -> Triple(Icons.Filled.Check, x.success, x.successBg)
                            "cancelled" -> Triple(Icons.Filled.Close, MaterialTheme.colorScheme.onSurfaceVariant, x.tile)
                            else -> Triple(Icons.Filled.Warning, x.warn, x.warnBg)
                        }
                        Row(Modifier.padding(horizontal = 16.dp, vertical = 14.dp), horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                            Box(Modifier.size(36.dp).clip(CircleShape).background(bg), contentAlignment = Alignment.Center) {
                                Icon(icon, null, tint = fg, modifier = Modifier.size(18.dp))
                            }
                            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                                    Text(r.headline(), fontWeight = FontWeight.SemiBold, fontSize = 15.sp, modifier = Modifier.weight(1f))
                                    Text(friendly(Instant.ofEpochMilli(r.startMs).atZone(zone)), fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                                Text(
                                    if (r.result == "ok") "${r.advice()} · ${formatDuration((r.endMs - r.startMs) / 1000)}" else r.advice(),
                                    fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun Stat(value: String, label: String, modifier: Modifier) {
    RCard(modifier, radius = 16.dp, padding = 12.dp) {
        Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(value, fontSize = 18.sp, fontWeight = FontWeight.Bold, maxLines = 1)
            Text(label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
