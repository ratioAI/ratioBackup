package de.ratio.backup.backup

import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Environment
import android.os.IBinder
import android.os.PowerManager
import de.ratio.backup.net.BackupApi
import de.ratio.backup.net.Discovery
import de.ratio.backup.net.Pairing
import de.ratio.backup.net.PinnedClient
import de.ratio.backup.scan.Scanner
import de.ratio.backup.schedule.AlarmScheduler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import java.io.File
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/** Vordergrund-Dienst, der einen Sicherungslauf ausführt und danach den nächsten plant. */
class BackupService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    @Volatile private var running = false

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        Notifier.ensureChannels(this)
        startForeground(Notifier.ID_PROGRESS, Notifier.progress(this, "Suche PC …"), ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
        if (running) return START_NOT_STICKY
        running = true
        scope.launch {
            val prefs = Prefs(this@BackupService)
            val wake = (getSystemService(Context.POWER_SERVICE) as PowerManager)
                .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "RatioBackup:run").apply { acquire(6 * 60 * 60 * 1000L) }
            try {
                val outcome = runOnce(prefs)
                record(prefs, outcome)
            } finally {
                wake.release()
                AlarmScheduler.scheduleNext(this@BackupService)
                Notifier.maybeStaleWarning(this@BackupService, prefs)
                running = false
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
        return START_NOT_STICKY
    }

    private suspend fun runOnce(prefs: Prefs): RunOutcome {
        val token = prefs.token
        val fingerprint = prefs.fingerprint
        if (token == null || fingerprint == null) return RunOutcome.NotPaired
        val pc = Discovery(this).find()
        if (pc == null || !pc.fingerprint.equals(fingerprint, ignoreCase = true)) return RunOutcome.PcNotFound
        val api = BackupApi(PinnedClient.create(fingerprint), Pairing.baseUrl(pc), token)
        val storage = Environment.getExternalStorageDirectory()
        val runner = BackupRunner({ Scanner(storage).scan() }, api, { File(storage, it.path) })
        val nm = getSystemService(NotificationManager::class.java)
        return runner.run { done, total ->
            nm.notify(Notifier.ID_PROGRESS, Notifier.progress(this, "Sichere … ($done von $total)", done, total))
        }
    }

    private fun record(prefs: Prefs, outcome: RunOutcome) {
        val time = DateTimeFormatter.ofPattern("dd.MM. HH:mm").withZone(ZoneId.systemDefault()).format(Instant.now())
        prefs.lastStatus = when (outcome) {
            is RunOutcome.Success -> {
                prefs.lastSuccess = Instant.now()
                "$time: erfolgreich (${outcome.uploaded} neu" + (if (outcome.skipped > 0) ", ${outcome.skipped} beim nächsten Mal" else "") + ")"
            }
            RunOutcome.NotPaired -> "$time: noch nicht mit dem PC gekoppelt"
            RunOutcome.PcNotFound -> "$time: PC nicht gefunden (aus oder anderes WLAN)"
            RunOutcome.QuotaFull -> {
                Notifier.warn(this, "PC-Speicher voll", "Das Speicherlimit am PC ist erreicht. Erhöhe es im RatioBackup-Programm am PC.")
                "$time: Speicherlimit am PC erreicht – Sicherung pausiert"
            }
            is RunOutcome.Failed -> "$time: abgebrochen (${outcome.reason}) – geht beim nächsten Mal weiter"
        }
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    companion object {
        fun start(context: Context) {
            context.startForegroundService(Intent(context, BackupService::class.java))
        }
    }
}
