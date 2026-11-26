package de.ratio.backup

import de.ratio.backup.scan.FileEntry
import de.ratio.backup.scan.PathFilter
import de.ratio.backup.scan.Scanner
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import java.io.File

class ScannerTest {
    @get:Rule
    val tmp = TemporaryFolder()

    private fun file(rel: String, content: String = "x", mtime: Long = 1_764_180_000_000): File =
        File(tmp.root, rel).apply {
            parentFile!!.mkdirs()
            writeText(content)
            setLastModified(mtime)
        }

    @Test
    fun scan_findsFilesUnderRoots() {
        file("DCIM/Camera/a.jpg", "abc")
        file("Download/b.pdf")
        file("Android/media/com.whatsapp/WhatsApp/Media/WhatsApp Images/c.jpg")
        file("Android/media/com.whatsapp/WhatsApp/Databases/msgstore.db.crypt14")
        val r = Scanner(tmp.root).scan()
        assertEquals(
            setOf(
                "DCIM/Camera/a.jpg", "Download/b.pdf",
                "Android/media/com.whatsapp/WhatsApp/Media/WhatsApp Images/c.jpg",
                "Android/media/com.whatsapp/WhatsApp/Databases/msgstore.db.crypt14",
            ),
            r.files.map { it.path }.toSet(),
        )
        val a = r.files.first { it.path == "DCIM/Camera/a.jpg" }
        assertEquals(FileEntry("DCIM/Camera/a.jpg", 3, 1_764_180_000_000), a)
    }

    @Test
    fun scan_ignoresOtherFolders() {
        file("Documents/x.txt")
        file("Android/data/com.foo/y")
        assertTrue(Scanner(tmp.root).scan().files.isEmpty())
    }

    @Test
    fun filter_excludesThumbnailsTrashedPendingAndNomedia() {
        assertFalse(PathFilter.include("DCIM/.thumbnails/1.jpg"))
        assertFalse(PathFilter.include("DCIM/Camera/.trashed-1767000000-IMG.jpg"))
        assertFalse(PathFilter.include("DCIM/Camera/.pending-1767000000-IMG.jpg"))
        assertFalse(PathFilter.include("Pictures/.nomedia"))
        assertFalse(PathFilter.include("Documents/a.txt"))
        assertTrue(PathFilter.include("DCIM/Camera/IMG.jpg"))
    }

    @Test
    fun scan_missingRoot_notInRoots() {
        file("DCIM/a.jpg")
        val r = Scanner(tmp.root).scan()
        assertTrue("DCIM" in r.roots)
        assertFalse("Android/media/com.whatsapp/WhatsApp/Media" in r.roots)
    }

    @Test
    fun scan_emptyExistingRoot_isInRoots() {
        File(tmp.root, "Movies").mkdirs()
        assertTrue("Movies" in Scanner(tmp.root).scan().roots)
    }

    @Test
    fun paths_useForwardSlash_relativeToStorageRoot() {
        file("Pictures/Screenshots/s.png")
        val p = Scanner(tmp.root).scan().files.single().path
        assertEquals("Pictures/Screenshots/s.png", p)
        assertFalse(p.startsWith("/"))
    }

    @Test
    fun roots_matchSpec() = assertEquals(
        listOf(
            "DCIM", "Pictures", "Movies", "Download",
            "Android/media/com.whatsapp/WhatsApp/Media",
            "Android/media/com.whatsapp/WhatsApp/Databases",
        ),
        PathFilter.ROOTS,
    )

    @Test
    fun scan_onlySelectedRoots() {
        file("DCIM/a.jpg")
        file("Download/b.pdf")
        val r = Scanner(tmp.root, listOf("DCIM")).scan()
        assertEquals(listOf("DCIM/a.jpg"), r.files.map { it.path })
        assertEquals(listOf("DCIM"), r.roots)
    }
}
