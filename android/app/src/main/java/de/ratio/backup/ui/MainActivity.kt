package de.ratio.backup.ui

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.lifecycle.compose.LifecycleResumeEffect
import de.ratio.backup.backup.Notifier
import de.ratio.backup.backup.Prefs
import de.ratio.backup.schedule.AlarmScheduler

private enum class Tab(val label: String, val icon: ImageVector) {
    Overview("Übersicht", Icons.Filled.Home),
    History("Verlauf", Icons.AutoMirrored.Filled.List),
    Settings("Einstellungen", Icons.Filled.Settings),
}

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        Notifier.ensureChannels(this)
        val prefs = Prefs(this)
        if (prefs.isPaired) AlarmScheduler.scheduleNext(this)

        setContent {
            RatioTheme {
                var refresh by remember { mutableIntStateOf(0) }
                var showSetup by remember { mutableStateOf(!prefs.isPaired || !Permissions.hasAllFiles()) }
                var tab by rememberSaveable { mutableStateOf(Tab.Overview) }
                LifecycleResumeEffect(Unit) {
                    refresh++
                    onPauseOrDispose { }
                }
                if (showSetup) {
                    Scaffold(containerColor = MaterialTheme.colorScheme.background) { pad ->
                        Box(Modifier.padding(pad)) {
                            SetupScreen(prefs, refresh, onDone = {
                                AlarmScheduler.scheduleNext(this@MainActivity)
                                showSetup = false
                            })
                        }
                    }
                } else {
                    Scaffold(
                        containerColor = MaterialTheme.colorScheme.background,
                        bottomBar = {
                            NavigationBar(containerColor = MaterialTheme.colorScheme.surface) {
                                Tab.entries.forEach { t ->
                                    NavigationBarItem(
                                        selected = tab == t,
                                        onClick = { tab = t },
                                        icon = { Icon(t.icon, contentDescription = null) },
                                        label = { Text(t.label) },
                                        colors = NavigationBarItemDefaults.colors(indicatorColor = MaterialTheme.colorScheme.primaryContainer),
                                    )
                                }
                            }
                        },
                    ) { pad ->
                        Box(Modifier.fillMaxSize().padding(pad)) {
                            when (tab) {
                                Tab.Overview -> OverviewScreen(prefs, refresh, onOpenSetup = { showSetup = true })
                                Tab.History -> HistoryScreen(prefs, refresh)
                                Tab.Settings -> SettingsScreen(
                                    prefs,
                                    onOpenSetup = { showSetup = true },
                                    onRepair = {
                                        prefs.token = null
                                        prefs.fingerprint = null
                                        showSetup = true
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
