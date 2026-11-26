package de.ratio.backup

import de.ratio.backup.backup.RunHistory
import de.ratio.backup.backup.RunRecord
import de.ratio.backup.update.UpdateInfo
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class HistoryAndUpdateTest {
    private fun rec(start: Long) = RunRecord(start, start + 1, 1, 0, 10, "ok", "")

    @Test
    fun add_keepsNewestFirst() {
        var j: String? = null
        j = RunHistory.add(j, rec(1))
        j = RunHistory.add(j, rec(2))
        assertEquals(listOf(2L, 1L), RunHistory.parse(j).map { it.startMs })
    }

    @Test
    fun add_capsAt30() {
        var j: String? = null
        for (i in 1..40) j = RunHistory.add(j, rec(i.toLong()))
        val l = RunHistory.parse(j)
        assertEquals(30, l.size)
        assertEquals(40L, l.first().startMs)
    }

    @Test
    fun parse_garbage_empty() = assertTrue(RunHistory.parse("{kaputt").isEmpty())

    @Test
    fun parse_null_empty() = assertTrue(RunHistory.parse(null).isEmpty())

    @Test
    fun update_parse_valid() {
        val u = UpdateInfo.parse("""{"versionCode":123,"versionName":"0.23","notes":"Neu","apk":"RatioBackup.apk","exe":"RatioBackup-PC.exe"}""")!!
        assertEquals(123, u.versionCode)
        assertEquals("0.23", u.versionName)
        assertEquals("Neu", u.notes)
    }

    @Test
    fun update_parse_invalid_null() {
        assertNull(UpdateInfo.parse("<html>404</html>"))
        assertNull(UpdateInfo.parse(""))
    }

    @Test
    fun update_isNewer_comparesCode() {
        val u = UpdateInfo(123, "0.23")
        assertTrue(u.isNewerThan(122))
        assertFalse(u.isNewerThan(123))
    }

    @Test
    fun update_apkUrl_built() = assertEquals(
        "https://github.com/ratioAI/ratioBackup/releases/download/test/RatioBackup.apk",
        UpdateInfo(1, "0.1").apkUrl,
    )
}
