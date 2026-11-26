package de.ratio.backup.backup

/** Momentaufnahme eines laufenden Uploads. */
data class Progress(
    val doneFiles: Int,
    val totalFiles: Int,
    val doneBytes: Long,
    val totalBytes: Long,
    val bytesPerSec: Long,
    val etaSec: Long?,
    val currentFile: String?,
) {
    /** Übertragene Bytes ÷ zu übertragende Bytes, 0–100. */
    val percent: Int
        get() = if (totalBytes <= 0) 100 else (doneBytes * 100 / totalBytes).coerceIn(0, 100).toInt()
}

/** Zählt Bytes und Dateien mit und schätzt Tempo (gleitend über 10 s) und Restzeit. */
class ProgressTracker(
    private val totalFiles: Int,
    totalBytes: Long,
    private val nowMs: () -> Long,
) {
    private var total = totalBytes
    private var done = 0L
    private var files = 0
    private var current: String? = null
    private val samples = ArrayDeque<Pair<Long, Long>>().apply { add(nowMs() to 0L) }

    @Synchronized fun addBytes(n: Long) {
        done += n
        val t = nowMs()
        if (t - samples.last().first >= SAMPLE_MS) samples.addLast(t to done)
        while (samples.size > 2 && t - samples[1].first > WINDOW_MS) samples.removeFirst()
    }

    /** Eine Datei wird übersprungen: ihre restlichen Bytes zählen nicht mehr zur Gesamtmenge. */
    @Synchronized fun dropBytes(n: Long) { total = (total - n).coerceAtLeast(done) }

    @Synchronized fun fileStarted(path: String) { current = path }

    @Synchronized fun fileDone() { files++ ; current = null }

    @Synchronized fun snapshot(): Progress {
        val (t0, b0) = samples.first()
        val dt = nowMs() - t0
        val speed = if (dt > 0) ((done - b0) * 1000 / dt).coerceAtLeast(0) else 0
        val remaining = (total - done).coerceAtLeast(0)
        val eta = if (speed > 0) remaining / speed else null
        return Progress(files, totalFiles, done, total, speed, eta, current)
    }

    private companion object {
        const val SAMPLE_MS = 250L
        const val WINDOW_MS = 10_000L
    }
}
