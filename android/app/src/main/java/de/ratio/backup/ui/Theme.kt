package de.ratio.backup.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

/** Zusatzfarben aus den Entwürfen, die Material 3 nicht kennt. */
data class Extra(val success: Color, val successBg: Color, val warn: Color, val warnBg: Color, val track: Color, val tile: Color)

val LocalExtra = staticCompositionLocalOf {
    Extra(Color(0xFF1F9D57), Color(0xFFE3F4EA), Color(0xFFE8A23A), Color(0xFFFFF4E0), Color(0xFFE7EBF3), Color(0xFFF3F5FA))
}

private val Light = lightColorScheme(
    primary = Color(0xFF1D5BD8), onPrimary = Color.White,
    primaryContainer = Color(0xFFDBE6FD), onPrimaryContainer = Color(0xFF12151C),
    background = Color(0xFFF6F7FB), onBackground = Color(0xFF12151C),
    surface = Color.White, onSurface = Color(0xFF12151C),
    surfaceVariant = Color(0xFFF3F5FA), onSurfaceVariant = Color(0xFF5B6272),
    surfaceContainer = Color.White, outline = Color(0xFFD5DAE5), outlineVariant = Color(0xFFE7EBF3),
    error = Color(0xFFB42318), errorContainer = Color(0xFFFDECEA), onErrorContainer = Color(0xFF7A1A12),
)

private val Dark = darkColorScheme(
    primary = Color(0xFF8DB4FF), onPrimary = Color(0xFF0E1117),
    primaryContainer = Color(0xFF233A66), onPrimaryContainer = Color(0xFFE8EBF2),
    background = Color(0xFF0E1117), onBackground = Color(0xFFE8EBF2),
    surface = Color(0xFF161A22), onSurface = Color(0xFFE8EBF2),
    surfaceVariant = Color(0xFF1E232D), onSurfaceVariant = Color(0xFF9AA3B5),
    surfaceContainer = Color(0xFF161A22), outline = Color(0xFF353C49), outlineVariant = Color(0xFF252B36),
    error = Color(0xFFF2877C), errorContainer = Color(0xFF3A1A17), onErrorContainer = Color(0xFFFFD9D4),
)

private val LightExtra = Extra(Color(0xFF1F9D57), Color(0xFFE3F4EA), Color(0xFFB7791F), Color(0xFFFFF4E0), Color(0xFFE7EBF3), Color(0xFFF3F5FA))
private val DarkExtra = Extra(Color(0xFF3CCF7F), Color(0xFF14301F), Color(0xFFF0B45A), Color(0xFF3A2A10), Color(0xFF252B36), Color(0xFF1E232D))

@Composable
fun RatioTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    androidx.compose.runtime.CompositionLocalProvider(LocalExtra provides if (dark) DarkExtra else LightExtra) {
        MaterialTheme(colorScheme = if (dark) Dark else Light, content = content)
    }
}
