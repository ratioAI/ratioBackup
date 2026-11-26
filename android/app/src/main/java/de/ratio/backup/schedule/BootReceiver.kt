package de.ratio.backup.schedule

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/** Nach Neustart oder Update den Wecker neu stellen. */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED -> AlarmScheduler.scheduleNext(context)
        }
    }
}
