package de.ratio.backup.backup

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import de.ratio.backup.schedule.Schedule
import de.ratio.backup.ui.MainActivity
import java.time.ZonedDateTime

object Notifier {
    const val CHANNEL_PROGRESS = "progress"
    const val CHANNEL_WARN = "warn"
    const val ID_PROGRESS = 1
    private const val ID_WARN = 2

    fun ensureChannels(context: Context) {
        val nm = context.getSystemService(NotificationManager::class.java)
        nm.createNotificationChannel(NotificationChannel(CHANNEL_PROGRESS, "Sicherung läuft", NotificationManager.IMPORTANCE_LOW))
        nm.createNotificationChannel(NotificationChannel(CHANNEL_WARN, "Warnungen", NotificationManager.IMPORTANCE_DEFAULT))
    }

    private fun openApp(context: Context): PendingIntent = PendingIntent.getActivity(
        context, 0, Intent(context, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE,
    )

    fun progress(context: Context, text: String, done: Int = 0, total: Int = 0): Notification =
        Notification.Builder(context, CHANNEL_PROGRESS)
            .setSmallIcon(android.R.drawable.stat_sys_upload)
            .setContentTitle("RatioBackup")
            .setContentText(text)
            .setOngoing(true)
            .setContentIntent(openApp(context))
            .apply { if (total > 0) setProgress(total, done, false) }
            .build()

    fun warn(context: Context, title: String, text: String) {
        if (context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return
        val n = Notification.Builder(context, CHANNEL_WARN)
            .setSmallIcon(android.R.drawable.stat_sys_warning)
            .setContentTitle(title)
            .setContentText(text)
            .setStyle(Notification.BigTextStyle().bigText(text))
            .setAutoCancel(true)
            .setContentIntent(openApp(context))
            .build()
        context.getSystemService(NotificationManager::class.java).notify(ID_WARN, n)
    }

    fun maybeStaleWarning(context: Context, prefs: Prefs) {
        if (Schedule.needsStaleWarning(ZonedDateTime.now(), prefs.lastSuccess))
            warn(context, "Seit 3 Tagen keine Sicherung", "War der PC abends an und das Handy im WLAN? Öffne RatioBackup für Details.")
    }
}
