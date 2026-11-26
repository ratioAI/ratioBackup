package de.ratio.backup

import de.ratio.backup.net.verifyCode
import de.ratio.backup.scan.Category
import de.ratio.backup.scan.rootsFor
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class CategoriesTest {
    @Test fun rootsFor_whatsapp_bothRoots() = assertEquals(
        listOf("Android/media/com.whatsapp/WhatsApp/Media", "Android/media/com.whatsapp/WhatsApp/Databases"),
        rootsFor(setOf(Category.WHATSAPP)),
    )
    @Test fun rootsFor_camera() = assertEquals(listOf("DCIM", "Movies"), rootsFor(setOf(Category.CAMERA)))
    @Test fun rootsFor_none_empty() = assertTrue(rootsFor(emptySet()).isEmpty())
    @Test fun rootsFor_all_coversEveryRoot() = assertEquals(
        de.ratio.backup.scan.PathFilter.ROOTS.toSet(), rootsFor(Category.entries.toSet()).toSet(),
    )
    @Test fun verifyCode_first4Upper() = assertEquals("4F2A", verifyCode("4f2a9c00ff"))
}
