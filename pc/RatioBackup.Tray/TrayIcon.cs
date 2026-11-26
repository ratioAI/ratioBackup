using System;
using System.Collections.Generic;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace RatioBackup.Tray;

/// <summary>Symbol unten rechts in der Taskleiste.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly HashSet<string> _shownOnce = new();

    public TrayIcon(Action open, Action quit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("RatioBackup öffnen", null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => quit());
        _icon = new Forms.NotifyIcon { Icon = CreateIcon(), Text = "RatioBackup", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => open();
        _icon.BalloonTipClicked += (_, _) => open();
    }

    public void SetTooltip(string text) => _icon.Text = text.Length > 63 ? text[..63] : text;

    public void Balloon(string title, string text) => _icon.ShowBalloonTip(8000, "RatioBackup – " + title, text, Forms.ToolTipIcon.Info);

    /// <summary>Nur einmal pro Programmstart zeigen (sonst nach jeder Datei).</summary>
    public void BalloonOnce(string key, string title, string text)
    {
        if (_shownOnce.Add(key)) Balloon(title, text);
    }

    private static Drawing.Icon CreateIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(29, 91, 216));
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Drawing.Font("Segoe UI", 15, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            Forms.TextRenderer.DrawText(g, "R", font, new Drawing.Rectangle(0, 0, 32, 32), Drawing.Color.White,
                Forms.TextFormatFlags.HorizontalCenter | Forms.TextFormatFlags.VerticalCenter);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
