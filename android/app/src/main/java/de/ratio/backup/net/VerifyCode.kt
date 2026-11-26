package de.ratio.backup.net

/** Prüfcode zum Abgleich beim Koppeln: die ersten 4 Zeichen des Zertifikat-Fingerabdrucks. */
fun verifyCode(fingerprint: String): String = fingerprint.take(4).uppercase()
