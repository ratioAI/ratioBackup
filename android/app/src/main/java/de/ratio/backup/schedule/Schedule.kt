package de.ratio.backup.schedule

import java.time.Duration
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZonedDateTime

/** Zeitplan: täglich ab 20:00, alle 5 Minuten bis 20:30, sonst am nächsten Tag. */
object Schedule {
    val START: LocalTime = LocalTime.of(20, 0)
    val END: LocalTime = LocalTime.of(20, 30)
    val RETRY: Duration = Duration.ofMinutes(5)
    val WINDOW: Duration = Duration.ofMinutes(30)
    val STALE: Duration = Duration.ofDays(3)

    /** Fenster: [start] bis [start] + 30 Min. */
    fun nextAttempt(now: ZonedDateTime, lastSuccessDate: LocalDate?, start: LocalTime = START): ZonedDateTime {
        val today = now.toLocalDate()
        val startToday = today.atTime(start).atZone(now.zone)
        val endToday = startToday.plus(WINDOW)
        val tomorrow = today.plusDays(1).atTime(start).atZone(now.zone)

        if (lastSuccessDate != null && !lastSuccessDate.isBefore(today)) return tomorrow
        if (now.isBefore(startToday) || now.isEqual(startToday)) return startToday
        if (now.isAfter(endToday)) return tomorrow
        val next = now.plus(RETRY).withSecond(0).withNano(0)
        return if (next.isAfter(endToday)) endToday.takeIf { it.isAfter(now) } ?: tomorrow else next
    }

    fun needsStaleWarning(now: ZonedDateTime, lastSuccess: Instant?): Boolean =
        lastSuccess != null && Duration.between(lastSuccess, now.toInstant()) > STALE
}
