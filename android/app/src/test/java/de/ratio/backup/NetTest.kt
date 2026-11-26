package de.ratio.backup

import de.ratio.backup.net.BackupApi
import de.ratio.backup.net.Pairing
import de.ratio.backup.net.PairingException
import de.ratio.backup.net.UploadResult
import de.ratio.backup.scan.FileEntry
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.io.File
import java.security.MessageDigest

class NetTest {
    @get:Rule
    val tmp = TemporaryFolder()
    private lateinit var server: MockWebServer
    private val client = OkHttpClient()

    @Before fun start() { server = MockWebServer().apply { start() } }
    @After fun stop() = server.shutdown()

    private fun api() = BackupApi(client, server.url("/"), "TOKEN")
    private fun sha(b: ByteArray) = MessageDigest.getInstance("SHA-256").digest(b).joinToString("") { "%02x".format(it) }

    @Test
    fun verifyProof_matchesPcHmac() {
        assertTrue(Pairing.verifyProof("123456", "abc", "f6ced6f4883ffc0981a6b9945819f680102b43097ad8ef7a0df9bde06fb3d2e4"))
        assertFalse(Pairing.verifyProof("123457", "abc", "f6ced6f4883ffc0981a6b9945819f680102b43097ad8ef7a0df9bde06fb3d2e4"))
    }

    @Test
    fun pair_ok_returnsToken() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"token":"T0K","proof":"${Pairing.proof("123456", "abc")}"}"""))
        assertEquals("T0K", Pairing.pair(client, server.url("/"), "abc", "123456", "Xiaomi 18 Pro"))
        val req = server.takeRequest()
        assertEquals("/pair", req.path)
        assertTrue(req.body.readUtf8().contains("\"code\":\"123456\""))
    }

    @Test
    fun pair_403_throws() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(403))
        try { Pairing.pair(client, server.url("/"), "abc", "111111", "x"); fail() } catch (e: PairingException) { }
    }

    @Test
    fun pair_badProof_throws() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"token":"T0K","proof":"00"}"""))
        try { Pairing.pair(client, server.url("/"), "abc", "123456", "x"); fail() } catch (e: PairingException) { }
    }

    @Test
    fun upload_sendsFromOffset() = runBlocking {
        val data = "0123456789".toByteArray()
        val f = tmp.newFile("v.mp4").apply { writeBytes(data) }
        server.enqueue(MockResponse().setResponseCode(200))
        val r = api().upload(f, FileEntry("DCIM/v.mp4", 10, 5), 4)
        assertEquals(UploadResult.Done, r)
        val req = server.takeRequest()
        assertEquals("PUT", req.method)
        assertEquals("456789", req.body.readUtf8())
        assertEquals("DCIM/v.mp4", req.requestUrl!!.queryParameter("path"))
        assertEquals("4", req.requestUrl!!.queryParameter("offset"))
        assertEquals("10", req.requestUrl!!.queryParameter("size"))
        assertEquals("5", req.requestUrl!!.queryParameter("mtimeMs"))
        assertEquals(sha(data), req.getHeader("X-Sha256"))
        assertEquals("Bearer TOKEN", req.getHeader("Authorization"))
    }

    @Test
    fun upload_206_returnsPartial() = runBlocking {
        val f = tmp.newFile("a").apply { writeBytes(ByteArray(10)) }
        server.enqueue(MockResponse().setResponseCode(206).setBody("""{"offset":6}"""))
        assertEquals(UploadResult.Partial(6), api().upload(f, FileEntry("DCIM/a", 10, 1), 0))
    }

    @Test
    fun upload_409_returnsWrongOffset() = runBlocking {
        val f = tmp.newFile("a").apply { writeBytes(ByteArray(10)) }
        server.enqueue(MockResponse().setResponseCode(409).setBody("""{"offset":3}"""))
        assertEquals(UploadResult.WrongOffset(3), api().upload(f, FileEntry("DCIM/a", 10, 1), 0))
    }

    @Test
    fun upload_507_returnsQuotaFull() = runBlocking {
        val f = tmp.newFile("a").apply { writeBytes(ByteArray(10)) }
        server.enqueue(MockResponse().setResponseCode(507))
        assertEquals(UploadResult.QuotaFull, api().upload(f, FileEntry("DCIM/a", 10, 1), 0))
    }

    @Test
    fun upload_422_returnsCorrupt() = runBlocking {
        val f = tmp.newFile("a").apply { writeBytes(ByteArray(10)) }
        server.enqueue(MockResponse().setResponseCode(422))
        assertEquals(UploadResult.Corrupt, api().upload(f, FileEntry("DCIM/a", 10, 1), 0))
    }

    @Test
    fun manifest_parsesNeeded() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"needed":[{"path":"DCIM/a","offset":7}],"trashed":1,"heldBackDeletions":0}"""))
        val r = api().manifest(listOf("DCIM"), listOf(FileEntry("DCIM/a", 9, 1)))
        assertEquals(7, r.needed.single().offset)
        assertEquals(1, r.trashed)
        assertTrue(server.takeRequest().body.readUtf8().contains("\"roots\":[\"DCIM\"]"))
    }

    @Test
    fun status_carriesBearerToken() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"usedBytes":5,"quotaBytes":10,"trashBytes":0,"lastBackupUtc":null,"pendingDeletionReview":true}"""))
        val s = api().status()
        assertEquals(5, s.usedBytes)
        assertTrue(s.pendingDeletionReview)
        assertEquals("Bearer TOKEN", server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun complete_posts() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(200))
        api().complete()
        val req = server.takeRequest()
        assertEquals("POST", req.method)
        assertEquals("/complete", req.path)
    }
}
