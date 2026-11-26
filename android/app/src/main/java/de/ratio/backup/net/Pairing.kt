package de.ratio.backup.net

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.security.MessageDigest
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

class PairingException(message: String) : Exception(message)

object Pairing {
    fun proof(code: String, fingerprint: String): String {
        val mac = Mac.getInstance("HmacSHA256").apply { init(SecretKeySpec(code.toByteArray(), "HmacSHA256")) }
        return mac.doFinal(fingerprint.toByteArray()).joinToString("") { "%02x".format(it) }
    }

    fun verifyProof(code: String, fingerprint: String, proof: String): Boolean =
        MessageDigest.isEqual(proof(code, fingerprint).toByteArray(), proof.lowercase().toByteArray())

    /** Koppelt mit dem PC; liefert das Token. Wirft [PairingException] bei falschem Code oder fremdem PC. */
    suspend fun pair(client: OkHttpClient, baseUrl: HttpUrl, fingerprint: String, code: String, deviceName: String): String =
        withContext(Dispatchers.IO) {
            val body = json.encodeToString(PairRequest.serializer(), PairRequest(code, deviceName))
                .toRequestBody("application/json".toMediaType())
            val req = Request.Builder().url(baseUrl.newBuilder().addPathSegment("pair").build()).post(body).build()
            client.newCall(req).execute().use { res ->
                if (res.code == 403) throw PairingException("Falscher oder abgelaufener Code")
                if (!res.isSuccessful) throw PairingException("PC antwortet mit Fehler ${res.code}")
                val r = json.decodeFromString(PairResponse.serializer(), res.body!!.string())
                if (!verifyProof(code, fingerprint, r.proof)) throw PairingException("Unbekannter PC – bitte erneut versuchen")
                r.token
            }
        }

    suspend fun pair(pc: PcInfo, code: String, deviceName: String): String =
        pair(PinnedClient.create(pc.fingerprint), baseUrl(pc), pc.fingerprint, code, deviceName)

    fun baseUrl(pc: PcInfo): HttpUrl = HttpUrl.Builder().scheme("https").host(pc.host).port(pc.port).build()
}
