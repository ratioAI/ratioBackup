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

    var historyJson: String?
        get() = sp.getString("history", null)
        set(v) = sp.edit().putString("history", v).apply()

    var lastUpdateCheck: Long
        get() = sp.getLong("lastUpdateCheck", 0)
        set(v) = sp.edit().putLong("lastUpdateCheck", v).apply()

    var updateJson: String?
        get() = sp.getString("updateJson", null)
        set(v) = sp.edit().putString("updateJson", v).apply()

    /** Startzeit der täglichen Sicherung. */
    var startTime: java.time.LocalTime
        get() = runCatching { java.time.LocalTime.parse(sp.getString("startTime", "20:00")) }.getOrDefault(java.time.LocalTime.of(20, 0))
        set(v) = sp.edit().putString("startTime", "%02d:%02d".format(v.hour, v.minute)).apply()

    /** Eingeschaltete Ordnergruppen (Standard: alle). */
    var categories: Set<de.ratio.backup.scan.Category>
        get() = sp.getStringSet("categories", null)
            ?.mapNotNull { n -> de.ratio.backup.scan.Category.entries.firstOrNull { it.name == n } }?.toSet()
            ?: de.ratio.backup.scan.Category.entries.toSet()
        set(v) = sp.edit().putStringSet("categories", v.map { it.name }.toSet()).apply()

    val isPaired: Boolean get() = token != null && fingerprint != null
}
