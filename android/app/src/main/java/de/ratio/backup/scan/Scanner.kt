package de.ratio.backup.scan

import kotlinx.serialization.Serializable
import java.io.File
import java.nio.file.Files
import java.nio.file.LinkOption

@Serializable
data class FileEntry(val path: String, val size: Long, val mtimeMs: Long)

data class ScanResult(val roots: List<String>, val files: List<FileEntry>)

/** Durchsucht die gesicherten Ordner unter [storageRoot] (normalerweise /storage/emulated/0). */
class Scanner(private val storageRoot: File, private val roots: List<String> = PathFilter.ROOTS) {
    fun scan(): ScanResult {
        val found = mutableListOf<String>()
        val files = mutableListOf<FileEntry>()
        for (root in roots) {
            val dir = File(storageRoot, root)
            // Nur lesbare Wurzeln melden: Der PC löscht ausschließlich innerhalb gemeldeter Wurzeln.
            if (!dir.isDirectory || !dir.canRead() || dir.list() == null) continue
            found += root
            dir.walkTopDown()
                .onEnter { !Files.isSymbolicLink(it.toPath()) }
                .filter { Files.isRegularFile(it.toPath(), LinkOption.NOFOLLOW_LINKS) }
                .forEach { f ->
                    val rel = f.relativeTo(storageRoot).invariantSeparatorsPath
                    if (PathFilter.include(rel)) files += FileEntry(rel, f.length(), f.lastModified())
                }
        }
        return ScanResult(found, files)
    }
}
