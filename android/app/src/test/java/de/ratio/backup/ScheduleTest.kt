package de.ratio.backup

import de.ratio.backup.schedule.Schedule
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Duration
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZonedDateTime

class ScheduleTest {
    private val zone = ZoneId.of("Europe/Berlin")
    private fun at(y: Int, m: Int, d: Int, h: Int, min: Int) = ZonedDateTime.of(y, m, d, h, min, 0, 0, zone)

    @Test fun before20_today2000() = assertEquals(at(2026, 11, 26, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 14, 0), null))
    @Test fun exactly2000_now() = assertEquals(at(2026, 11, 26, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 20, 0), null))
    @Test fun inWindow_noSuccess_plus5() = assertEquals(at(2026, 11, 26, 20, 12), Schedule.nextAttempt(at(2026, 11, 26, 20, 7), null))
    @Test fun inWindow_lastRetryAt2030() = assertEquals(at(2026, 11, 26, 20, 30), Schedule.nextAttempt(at(2026, 11, 26, 20, 27), null))
    @Test fun after2030_tomorrow2000() = assertEquals(at(2026, 11, 27, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 20, 31), null))
    @Test fun successToday_tomorrow2000() =
        assertEquals(at(2026, 11, 27, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 20, 5), LocalDate.of(2026, 11, 26)))
    @Test fun successToday_morning_stillTomorrow() =
        assertEquals(at(2026, 11, 27, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 10, 0), LocalDate.of(2026, 11, 26)))
    @Test fun successYesterday_today2000() =
        assertEquals(at(2026, 11, 26, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 10, 0), LocalDate.of(2026, 11, 25)))
    @Test fun at2359_tomorrow2000() = assertEquals(at(2026, 11, 27, 20, 0), Schedule.nextAttempt(at(2026, 11, 26, 23, 59), null))
    @Test fun dstSpring_2027_03_28() {
        val next = Schedule.nextAttempt(at(2027, 3, 27, 21, 0), null)
        assertEquals(at(2027, 3, 28, 20, 0), next)
        assertEquals(2, next.offset.totalSeconds / 3600)
    }
    @Test fun dstAutumn_2027_10_31() {
        val next = Schedule.nextAttempt(at(2027, 10, 30, 21, 0), null)
        assertEquals(at(2027, 10, 31, 20, 0), next)
        assertEquals(1, next.offset.totalSeconds / 3600)
    }
    @Test fun staleWarning_after3Days() {
        val now = at(2026, 11, 26, 20, 0)
        assertTrue(Schedule.needsStaleWarning(now, now.toInstant().minus(Duration.ofDays(3).plusMinutes(1))))
        assertFalse(Schedule.needsStaleWarning(now, now.toInstant().minus(Duration.ofDays(2).plusHours(23))))
    }
    @Test fun staleWarning_neverBackedUp_false() = assertFalse(Schedule.needsStaleWarning(at(2026, 11, 26, 20, 0), null))

    private val seven = java.time.LocalTime.of(19, 15)
    @Test fun customTime_before_sameDay() = assertEquals(at(2026, 11, 26, 19, 15), Schedule.nextAttempt(at(2026, 11, 26, 12, 0), null, seven))
    @Test fun customTime_window_plus5() = assertEquals(at(2026, 11, 26, 19, 25), Schedule.nextAttempt(at(2026, 11, 26, 19, 20), null, seven))
    @Test fun customTime_afterWindow_tomorrow() = assertEquals(at(2026, 11, 27, 19, 15), Schedule.nextAttempt(at(2026, 11, 26, 19, 46), null, seven))
    @Test fun customTime_successToday_tomorrowAtNewTime() =
        assertEquals(at(2026, 11, 27, 19, 15), Schedule.nextAttempt(at(2026, 11, 26, 10, 0), LocalDate.of(2026, 11, 26), seven))
}
