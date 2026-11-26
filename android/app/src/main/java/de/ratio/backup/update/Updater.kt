package de.ratio.backup.update

import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.net.Uri
import android.provider.Settings
import de.ratio.backup.BuildConfig
import de.ratio.backup.backup.Prefs
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

/** Prüft auf neue Versionen (Release "test") und installiert sie über den PackageInstaller. */
object Updater {
    private const val CHECK_EVERY_MS = 6 * 60 * 60 * 1000L
    private val http = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .build()

    val currentVersionCode: Int get() = BuildConfig.VERSION_CODE
    val currentVersionName: String get() = BuildConfig.VERSION_NAME

    /** Liefert eine neuere Version oder null. Ohne [force] höchstens alle 6 Stunden online. */
    suspend fun check(context: Context, force: Boolean = false): UpdateInfo? = withContext(Dispatchers.IO) {
        val prefs = Prefs(context)
        val now = System.currentTimeMillis()
        val cached = prefs.updateJson?.let(UpdateInfo::parse)
        if (!force && now - prefs.lastUpdateCheck < CHECK_EVERY_MS) return@withContext cached?.takeIf { it.isNewerThan(currentVersionCode) }
        val info = try {
            http.newCall(Request.Builder().url(UpdateInfo.VERSION_URL).build()).execute().use { res ->
                if (!res.isSuccessful) null else res.body?.string()?.let(UpdateInfo::parse)
            }
        } catch (e: IOException) {
            null
        }
        prefs.lastUpdateCheck = now
        if (info != null) prefs.updateJson = UpdateInfo.serializerJson(info)
        info?.takeIf { it.isNewerThan(currentVersionCode) }
    }

    fun canInstall(context: Context): Boolean = context.packageManager.canRequestPackageInstalls()

    fun openInstallPermission(context: Context) {
        context.startActivity(
            Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
        )
    }

    /** Lädt die APK (Fortschritt 0–100) und startet die Installation. */
    suspend fun downloadAndInstall(context: Context, info: UpdateInfo, onProgress: (Int) -> Unit) = withContext(Dispatchers.IO) {
        val apk = File(context.cacheDir, "update.apk")
        http.newCall(Request.Builder().url(info.apkUrl).build()).execute().use { res ->
            if (!res.isSuccessful) throw IOException("Download fehlgeschlagen (HTTP ${res.code})")
            val body = res.body ?: throw IOException("Leere Antwort")
            val total = body.contentLength()
            body.byteStream().use { input ->
                apk.outputStream().use { out ->
                    val buf = ByteArray(64 * 1024)
                    var done = 0L
                    while (true) {
                        val n = input.read(buf)
                        if (n < 0) break
                        out.write(buf, 0, n)
                        done += n
                        if (total > 0) onProgress((done * 100 / total).toInt())
                    }
                }
            }
        }
        val installer = context.packageManager.packageInstaller
        val params = PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL).apply {
            setAppPackageName(context.packageName)
            setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED)
        }
        val id = installer.createSession(params)
        installer.openSession(id).use { session ->
            session.openWrite("RatioBackup.apk", 0, apk.length()).use { out ->
                apk.inputStream().use { it.copyTo(out) }
                session.fsync(out)
            }
            val pi = PendingIntent.getBroadcast(
                context, id, Intent(context, InstallReceiver::class.java),
                PendingIntent.FLAG_MUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
            )
            session.commit(pi.intentSender)
        }
        apk.delete()
    }
}

/** Zeigt Androids Bestätigung „Installieren?", wenn das System sie verlangt. */
class InstallReceiver : android.content.BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when (val status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, -999)) {
            PackageInstaller.STATUS_PENDING_USER_ACTION -> {
                @Suppress("DEPRECATION")
                val confirm = intent.getParcelableExtra<Intent>(Intent.EXTRA_INTENT) ?: return
                context.startActivity(confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            }
            PackageInstaller.STATUS_SUCCESS -> {}
            PackageInstaller.STATUS_FAILURE_ABORTED -> {} // vom Nutzer abgebrochen
            else -> de.ratio.backup.backup.Notifier.warn(
                context, "Update nicht installiert",
                "Android meldet: ${intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE) ?: "Fehler $status"}. " +
                    "Lade die APK notfalls über den Link in der Anleitung neu.",
            )
        }
    }
}
