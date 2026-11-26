package de.ratio.backup.scan

/** Welche Ordner gesichert werden und welche Dateien darin übersprungen werden. */
object PathFilter {
    val ROOTS: List<String> = listOf(
        "DCIM",
        "Pictures",
        "Movies",
        "Download",
        "Android/media/com.whatsapp/WhatsApp/Media",
        "Android/media/com.whatsapp/WhatsApp/Databases",
    )

    fun include(relPath: String): Boolean {
        if (ROOTS.none { relPath.startsWith("$it/") }) return false
        val parts = relPath.split('/')
        if (parts.any { it == ".thumbnails" }) return false
        val name = parts.last()
        if (name == ".nomedia") return false
        if (name.startsWith(".trashed-") || name.startsWith(".pending-")) return false
        return true
    }
}
