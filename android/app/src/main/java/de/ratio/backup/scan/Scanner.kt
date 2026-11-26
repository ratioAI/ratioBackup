package de.ratio.backup.scan

import kotlinx.serialization.Serializable
import java.io.File
import java.nio.file.Files
import java.nio.file.LinkOption

@Serializable
data class FileEntry(val path: String, val size: Long, val mtimeMs: Long)

data class ScanResult(val roots: List<String>, val files: List<FileEntry>)

/** Durchsucht die gesicherten Ordner unter [storageRoot] (normalerweise /storage/emulated/0). */
class Scanner(private val storageRoot: File) {
    fun scan(): ScanResult {
        val roots = mutableListOf<String>()
        val files = mutableListOf<FileEntry>()
        for (root in PathFilter.ROOTS) {
            val dir = File(storageRoot, root)
            // Nur lesbare Wurzeln melden: Der PC löscht ausschließlich innerhalb gemeldeter Wurzeln.
            if (!dir.isDirectory || !dir.canRead() || dir.list() == null) continue
            roots += root
            dir.walkTopDown()
                .onEnter { !Files.isSymbolicLink(it.toPath()) }
                .filter { Files.isRegularFile(it.toPath(), LinkOption.NOFOLLOW_LINKS) }
                .forEach { f ->
                    val rel = f.relativeTo(storageRoot).invariantSeparatorsPath
                    if (PathFilter.include(rel)) files += FileEntry(rel, f.length(), f.lastModified())
                }
        }
        return ScanResult(roots, files)
    }
}
