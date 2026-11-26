package de.ratio.backup.ui

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.foundation.layout.fillMaxSize
import androidx.lifecycle.compose.LifecycleResumeEffect
import de.ratio.backup.backup.Notifier
import de.ratio.backup.backup.Prefs
import de.ratio.backup.schedule.AlarmScheduler

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Notifier.ensureChannels(this)
        val prefs = Prefs(this)
        if (prefs.isPaired) AlarmScheduler.scheduleNext(this)

        setContent {
            val dark = isSystemInDarkTheme()
            MaterialTheme(colorScheme = if (dark) dynamicDarkColorScheme(this) else dynamicLightColorScheme(this)) {
                Surface(Modifier.fillMaxSize()) {
                    var refresh by remember { mutableIntStateOf(0) }
                    var showSetup by remember { mutableStateOf(!prefs.isPaired || !Permissions.hasAllFiles()) }
                    LifecycleResumeEffect(Unit) {
                        refresh++
                        onPauseOrDispose { }
                    }
                    if (showSetup) {
                        SetupScreen(prefs, refresh, onDone = {
                            AlarmScheduler.scheduleNext(this@MainActivity)
                            showSetup = false
                        })
                    } else {
                        MainScreen(prefs, refresh, onOpenSetup = { showSetup = true })
                    }
                }
            }
        }
    }
}
