package de.ratio.backup

import de.ratio.backup.backup.ProgressTracker
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ProgressTrackerTest {
    private var now = 0L
    private fun tracker(files: Int, bytes: Long) = ProgressTracker(files, bytes) { now }

    @Test
    fun percent_fromBytes() {
        val t = tracker(1, 100)
        t.addBytes(25)
        assertEquals(25, t.snapshot().percent)
    }

    @Test
    fun zeroBytes_is100Percent() {
        val p = tracker(0, 0).snapshot()
        assertEquals(100, p.percent)
        assertNull(p.etaSec)
    }

    @Test
    fun speed_isBytesPerSecond() {
        val t = tracker(1, 10_000_000)
        now = 2_000
        t.addBytes(1_000_000)
        val s = t.snapshot().bytesPerSec
        assertTrue("speed=$s", s in 495_000..505_000)
    }

    @Test
    fun eta_fromRemainingAndSpeed() {
        val t = tracker(1, 3_000_000)
        now = 2_000
        t.addBytes(1_000_000)
        assertEquals(4L, t.snapshot().etaSec)
    }

    @Test
    fun eta_neverNegative_orInfinite() {
        val t = tracker(1, 100)
        assertNull(t.snapshot().etaSec)
        now = -5_000
        t.addBytes(10)
        val eta = t.snapshot().etaSec
        assertTrue(eta == null || eta >= 0)
    }

    @Test
    fun files_countAndCurrent() {
        val t = tracker(2, 10)
        t.fileStarted("DCIM/a.jpg")
        assertEquals("DCIM/a.jpg", t.snapshot().currentFile)
        t.fileDone()
        assertEquals(1, t.snapshot().doneFiles)
        assertEquals(2, t.snapshot().totalFiles)
    }

    @Test
    fun percent_neverAbove100() {
        val t = tracker(1, 10)
        t.addBytes(50)
        assertEquals(100, t.snapshot().percent)
    }
}
