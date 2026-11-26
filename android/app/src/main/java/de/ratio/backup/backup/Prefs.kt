package de.ratio.backup.backup

import android.content.Context
import java.time.Instant

/** Gespeicherter Zustand der App. */
class Prefs(context: Context) {
    private val sp = context.applicationContext.getSharedPreferences("ratiobackup", Context.MODE_PRIVATE)

    var token: String?
        get() = sp.getString("token", null)
        set(v) = sp.edit().putString("token", v).apply()

    var fingerprint: String?
        get() = sp.getString("fingerprint", null)
        set(v) = sp.edit().putString("fingerprint", v).apply()

    var pcName: String?
        get() = sp.getString("pcName", null)
        set(v) = sp.edit().putString("pcName", v).apply()

    var lastSuccess: Instant?
        get() = sp.getLong("lastSuccess", 0).takeIf { it > 0 }?.let(Instant::ofEpochMilli)
        set(v) = sp.edit().putLong("lastSuccess", v?.toEpochMilli() ?: 0).apply()

    var lastStatus: String?
        get() = sp.getString("lastStatus", null)
        set(v) = sp.edit().putString("lastStatus", v).apply()

    var autostartConfirmed: Boolean
        get() = sp.getBoolean("autostartConfirmed", false)
        set(v) = sp.edit().putBoolean("autostartConfirmed", v).apply()

    val isPaired: Boolean get() = token != null && fingerprint != null
}
