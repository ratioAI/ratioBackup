package de.ratio.backup.net

import android.content.Context
import android.net.wifi.WifiManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketTimeoutException

/** Findet den PC im Heimnetz per UDP-Broadcast (Port 47320). */
class Discovery(private val context: Context) {
    @Serializable private data class Reply(val name: String, val port: Int, val fingerprint: String)

    suspend fun find(timeoutMs: Long = 3000): PcInfo? = withContext(Dispatchers.IO) {
        val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        val lock = wifi.createMulticastLock("RatioBackup").apply { setReferenceCounted(false); acquire() }
        try {
            DatagramSocket().use { socket ->
                socket.broadcast = true
                socket.soTimeout = timeoutMs.toInt()
                val probe = PROBE.toByteArray(Charsets.US_ASCII)
                repeat(2) {
                    socket.send(DatagramPacket(probe, probe.size, InetAddress.getByName("255.255.255.255"), PORT))
                }
                val buf = ByteArray(2048)
                val packet = DatagramPacket(buf, buf.size)
                try {
                    socket.receive(packet)
                } catch (e: SocketTimeoutException) {
                    return@withContext null
                }
                val r = json.decodeFromString(Reply.serializer(), String(packet.data, 0, packet.length, Charsets.UTF_8))
                PcInfo(packet.address.hostAddress ?: return@withContext null, r.port, r.name, r.fingerprint)
            }
        } catch (e: Exception) {
            null
        } finally {
            lock.release()
        }
    }

    companion object {
        const val PORT = 47320
        const val PROBE = "RATIOBACKUP?"
    }
}
