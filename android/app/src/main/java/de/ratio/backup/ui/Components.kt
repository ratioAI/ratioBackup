package de.ratio.backup.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Abgerundete Karte wie in den Entwürfen. */
@Composable
fun RCard(
    modifier: Modifier = Modifier,
    color: Color = MaterialTheme.colorScheme.surface,
    radius: Dp = 20.dp,
    padding: Dp = 18.dp,
    content: @Composable ColumnScope.() -> Unit,
) {
    Surface(modifier.fillMaxWidth(), shape = RoundedCornerShape(radius), color = color) {
        Column(Modifier.padding(padding), verticalArrangement = Arrangement.spacedBy(10.dp), content = content)
    }
}

@Composable
fun ScreenTitle(kicker: String, title: String, trailing: @Composable () -> Unit = {}) {
    Row(Modifier.fillMaxWidth().padding(start = 20.dp, end = 20.dp, top = 20.dp, bottom = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(kicker, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, fontWeight = FontWeight.Medium)
            Text(title, fontSize = 26.sp, fontWeight = FontWeight.Bold, letterSpacing = (-0.5).sp)
        }
        trailing()
    }
}

@Composable
fun StatusPill(text: String, ok: Boolean) {
    val x = LocalExtra.current
    Row(
        Modifier.clip(CircleShape).background(if (ok) x.successBg else x.warnBg).padding(horizontal = 10.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Box(Modifier.size(8.dp).clip(CircleShape).background(if (ok) x.success else x.warn))
        Text(text, fontSize = 12.sp, fontWeight = FontWeight.SemiBold, color = if (ok) x.success else x.warn)
    }
}

@Composable
fun ProgressRing(percent: Int, size: Dp = 196.dp, center: @Composable () -> Unit) {
    Box(contentAlignment = Alignment.Center) {
        CircularProgressIndicator(
            progress = { percent / 100f },
            modifier = Modifier.size(size),
            strokeWidth = 16.dp,
            trackColor = LocalExtra.current.track,
            strokeCap = StrokeCap.Round,
        )
        Column(horizontalAlignment = Alignment.CenterHorizontally) { center() }
    }
}

@Composable
fun StatTile(value: String, label: String, modifier: Modifier = Modifier) {
    Column(
        modifier.clip(RoundedCornerShape(14.dp)).background(LocalExtra.current.tile).padding(vertical = 10.dp, horizontal = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        Text(value, fontSize = 16.sp, fontWeight = FontWeight.SemiBold, maxLines = 1)
        Text(label, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

/** Gestapelter Balken: Sicherung (Akzent) + Papierkorb (Warnfarbe) im Verhältnis zum Limit. */
@Composable
fun StorageBar(mirror: Long, trash: Long, quota: Long) {
    val q = quota.coerceAtLeast(1).toFloat()
    val m = (mirror / q).coerceIn(0f, 1f)
    val t = (trash / q).coerceIn(0f, 1f - m)
    Row(Modifier.fillMaxWidth().height(10.dp).clip(CircleShape).background(LocalExtra.current.track)) {
        if (m > 0f) Box(Modifier.fillMaxHeight().weight(m).background(MaterialTheme.colorScheme.primary))
        if (t > 0f) Box(Modifier.fillMaxHeight().weight(t).background(LocalExtra.current.warn))
        if (1f - m - t > 0f) Box(Modifier.fillMaxHeight().weight(1f - m - t))
    }
}

@Composable
fun Legend(color: Color, text: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        Box(Modifier.size(8.dp).clip(CircleShape).background(color))
        Text(text, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun IconBadge(bg: Color, content: @Composable () -> Unit) {
    Box(Modifier.size(40.dp).clip(RoundedCornerShape(12.dp)).background(bg), contentAlignment = Alignment.Center) { content() }
}
