package de.ratio.backup.schedule

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import de.ratio.backup.backup.BackupService
import de.ratio.backup.backup.Prefs
import java.time.ZoneId
import java.time.ZonedDateTime

/** Stellt den Wecker für den nächsten Sicherungsversuch. */
object AlarmScheduler {
    fun next(context: Context): ZonedDateTime {
        val zone = ZoneId.systemDefault()
        val lastDate = Prefs(context).lastSuccess?.atZone(zone)?.toLocalDate()
        return Schedule.nextAttempt(ZonedDateTime.now(zone), lastDate)
    }

    fun scheduleNext(context: Context) {
        val at = next(context)
        val am = context.getSystemService(AlarmManager::class.java)
        val pi = PendingIntent.getBroadcast(
            context, 0, Intent(context, AlarmReceiver::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val millis = at.toInstant().toEpochMilli()
        if (am.canScheduleExactAlarms()) am.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, millis, pi)
        else am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, millis, pi)
    }
}

class AlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        // Erst den nächsten Versuch planen, dann starten – so reißt die Kette nie ab.
        AlarmScheduler.scheduleNext(context)
        try {
            BackupService.start(context)
        } catch (_: Exception) {
        }
    }
}
