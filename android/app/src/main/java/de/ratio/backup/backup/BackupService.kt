package de.ratio.backup.backup

import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.net.wifi.WifiManager
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
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import java.io.File
import java.time.Instant

/** Vordergrund-Dienst: führt einen Sicherungslauf aus, meldet den Fortschritt live und plant den nächsten. */
class BackupService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    @Volatile private var job: Job? = null
    @Volatile private var api: BackupApi? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_CANCEL) {
            api?.cancelCurrent()
            job?.cancel()
            return START_NOT_STICKY
        }
        // Ersatz-Wecker sofort stellen: Stirbt der Prozess mitten im Lauf, geht die Kette trotzdem weiter.
        AlarmScheduler.scheduleNext(this)
        Notifier.ensureChannels(this)
        startForeground(Notifier.ID_PROGRESS, Notifier.running(this, "Suche PC …", null), ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
        if (job?.isActive == true) return START_NOT_STICKY

        job = scope.launch {
            val prefs = Prefs(this@BackupService)
            val start = System.currentTimeMillis()
            val wake = (getSystemService(Context.POWER_SERVICE) as PowerManager)
                .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "RatioBackup:run").apply { acquire(6 * 60 * 60 * 1000L) }
            val wifi = (applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager)
                .createWifiLock(WifiManager.WIFI_MODE_FULL_LOW_LATENCY, "RatioBackup:wifi").apply { acquire() }
            var lastProgress: Progress? = null
            try {
                val outcome = runOnce(prefs) { lastProgress = it }
                val record = toRecord(start, outcome, lastProgress)
                if (outcome is RunOutcome.Success) prefs.lastSuccess = Instant.now()
                prefs.historyJson = RunHistory.add(prefs.historyJson, record)
                prefs.lastStatus = record.headline()
                when (outcome) {
                    RunOutcome.QuotaFull -> Notifier.warn(this@BackupService, record.headline(), record.advice())
                    RunOutcome.NoStorageAccess -> Notifier.warn(this@BackupService, record.headline(), record.advice())
                    else -> {}
                }
                BackupState.set(UiState.Finished(record))
            } finally {
                wake.release()
                wifi.release()
                api = null
                AlarmScheduler.scheduleNext(this@BackupService)
                Notifier.maybeStaleWarning(this@BackupService, prefs)
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
        return START_NOT_STICKY
    }

    private suspend fun runOnce(prefs: Prefs, onProgress: (Progress) -> Unit): RunOutcome {
        val token = prefs.token
        val fingerprint = prefs.fingerprint
        if (token == null || fingerprint == null) return RunOutcome.NotPaired
        if (!Environment.isExternalStorageManager()) return RunOutcome.NoStorageAccess
        BackupState.set(UiState.Searching)
        val pc = Discovery(this).find()
        if (pc == null || !pc.fingerprint.equals(fingerprint, ignoreCase = true)) return RunOutcome.PcNotFound
        val client = BackupApi(PinnedClient.create(fingerprint), Pairing.baseUrl(pc), token)
        api = client
        BackupState.set(UiState.Comparing)
        notify("Gleiche mit dem PC ab …", null)
        val storage = Environment.getExternalStorageDirectory()
        val runner = BackupRunner(
            { Scanner(storage).scan() }, client, { File(storage, it.path) }, { Environment.isExternalStorageManager() },
        )
        return runner.run { p ->
            onProgress(p)
            BackupState.set(UiState.Uploading(p))
            notify("${p.percent} % · ${p.doneFiles} von ${p.totalFiles} Dateien", p)
        }
    }

    private fun notify(text: String, p: Progress?) =
        getSystemService(NotificationManager::class.java).notify(Notifier.ID_PROGRESS, Notifier.running(this, text, p))

    private fun toRecord(start: Long, o: RunOutcome, p: Progress?): RunRecord {
        val end = System.currentTimeMillis()
        val bytes = p?.doneBytes ?: 0
        return when (o) {
            is RunOutcome.Success -> RunRecord(start, end, o.uploaded, o.skipped, bytes, "ok", "")
            RunOutcome.PcNotFound -> RunRecord(start, end, p?.doneFiles ?: 0, 0, bytes, "pc_not_found", "")
            RunOutcome.QuotaFull -> RunRecord(start, end, p?.doneFiles ?: 0, 0, bytes, "quota", "")
            RunOutcome.NoStorageAccess -> RunRecord(start, end, 0, 0, 0, "no_access", "")
            RunOutcome.NotPaired -> RunRecord(start, end, 0, 0, 0, "not_paired", "")
            RunOutcome.Cancelled -> RunRecord(start, end, p?.doneFiles ?: 0, 0, bytes, "cancelled", "")
            is RunOutcome.Failed -> RunRecord(start, end, p?.doneFiles ?: 0, 0, bytes, "failed", o.reason)
        }
    }

    /** Android 15+: Zeitlimit für dataSync erreicht – sauber beenden, der Ersatz-Wecker steht schon. */
    override fun onTimeout(startId: Int, fgsType: Int) {
        api?.cancelCurrent()
        job?.cancel()
        AlarmScheduler.scheduleNext(this)
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        scope.cancel()
        if (BackupState.isRunning) BackupState.set(UiState.Idle)
        super.onDestroy()
    }

    companion object {
        const val ACTION_CANCEL = "de.ratio.backup.CANCEL"

        fun start(context: Context) {
            context.startForegroundService(Intent(context, BackupService::class.java))
        }

        fun cancel(context: Context) {
            context.startService(Intent(context, BackupService::class.java).setAction(ACTION_CANCEL))
        }
    }
}
