package de.ratio.backup.scan

/** Ordnergruppen, die der Nutzer einzeln an- und abschalten kann. */
enum class Category(val label: String, val roots: List<String>) {
    CAMERA("Kamera (Fotos & Videos)", listOf("DCIM", "Movies")),
    PICTURES("Bilder & Screenshots", listOf("Pictures")),
    DOWNLOADS("Downloads", listOf("Download")),
    WHATSAPP("WhatsApp", listOf("Android/media/com.whatsapp/WhatsApp/Media", "Android/media/com.whatsapp/WhatsApp/Databases")),
}

/** Wurzeln der eingeschalteten Kategorien, in fester Reihenfolge. */
fun rootsFor(enabled: Set<Category>): List<String> = Category.entries.filter { it in enabled }.flatMap { it.roots }
