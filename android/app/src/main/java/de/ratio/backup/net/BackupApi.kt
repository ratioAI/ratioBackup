package de.ratio.backup.net

import de.ratio.backup.scan.FileEntry
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl
import okhttp3.MediaType
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import okio.BufferedSink
import java.io.File
import java.io.IOException
import java.io.RandomAccessFile
import java.security.MessageDigest

class BackupApi(
    private val client: OkHttpClient,
    private val baseUrl: HttpUrl,
    private val token: String,
) : BackupApiPort {

    private fun url(segment: String) = baseUrl.newBuilder().addPathSegment(segment)
    private fun request(b: HttpUrl.Builder) = Request.Builder().url(b.build()).header("Authorization", "Bearer $token")
    private val jsonType = "application/json".toMediaType()

    override suspend fun status(): Status = withContext(Dispatchers.IO) {
        client.newCall(request(url("status")).get().build()).execute().use { res ->
            if (!res.isSuccessful) throw IOException("Status: HTTP ${res.code}")
            json.decodeFromString(Status.serializer(), res.body!!.string())
        }
    }

    override suspend fun manifest(roots: List<String>, files: List<FileEntry>): ManifestResult = withContext(Dispatchers.IO) {
        val body = json.encodeToString(ManifestRequest.serializer(), ManifestRequest(roots, files)).toRequestBody(jsonType)
        client.newCall(request(url("manifest")).post(body).build()).execute().use { res ->
            if (!res.isSuccessful) throw IOException("Abgleich: HTTP ${res.code}")
            json.decodeFromString(ManifestResult.serializer(), res.body!!.string())
        }
    }

    override suspend fun upload(file: File, entry: FileEntry, offset: Long): UploadResult = withContext(Dispatchers.IO) {
        val u = url("files")
            .addQueryParameter("path", entry.path)
            .addQueryParameter("size", entry.size.toString())
            .addQueryParameter("mtimeMs", entry.mtimeMs.toString())
            .addQueryParameter("offset", offset.toString())
        val req = request(u)
            .header("X-Sha256", sha256(file))
            .put(FileTailBody(file, offset, entry.size - offset))
            .build()
        client.newCall(req).execute().use { res ->
            when (res.code) {
                200 -> UploadResult.Done
                206 -> UploadResult.Partial(offsetOf(res.body?.string()))
                409 -> UploadResult.WrongOffset(offsetOf(res.body?.string()))
                422 -> UploadResult.Corrupt
                507 -> UploadResult.QuotaFull
                else -> throw PcErrorException(res.code)
            }
        }
    }

    override suspend fun complete() = withContext(Dispatchers.IO) {
        client.newCall(request(url("complete")).post(ByteArray(0).toRequestBody(null)).build()).execute().use { res ->
            if (!res.isSuccessful) throw IOException("Abschluss: HTTP ${res.code}")
        }
    }

    private fun offsetOf(body: String?): Long = json.decodeFromString(OffsetDto.serializer(), body ?: "{}").offset

    private class FileTailBody(private val file: File, private val offset: Long, private val length: Long) : RequestBody() {
        override fun contentType(): MediaType = "application/octet-stream".toMediaType()
        override fun contentLength(): Long = length
        override fun writeTo(sink: BufferedSink) {
            RandomAccessFile(file, "r").use { raf ->
                raf.seek(offset)
                val buf = ByteArray(64 * 1024)
                var left = length
                while (left > 0) {
                    val n = raf.read(buf, 0, minOf(buf.size.toLong(), left).toInt())
                    if (n < 0) throw FileChangedException("Datei wurde während des Hochladens kürzer")
                    sink.write(buf, 0, n)
                    left -= n
                }
            }
        }
    }

    companion object {
        fun sha256(file: File): String {
            val md = MessageDigest.getInstance("SHA-256")
            file.inputStream().use { input ->
                val buf = ByteArray(64 * 1024)
                while (true) {
                    val n = input.read(buf)
                    if (n < 0) break
                    md.update(buf, 0, n)
                }
            }
            return md.digest().joinToString("") { "%02x".format(it) }
        }
    }
}
