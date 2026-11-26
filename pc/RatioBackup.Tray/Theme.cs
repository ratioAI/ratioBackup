using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace RatioBackup.Tray;

/// <summary>Farben aus den Entwürfen; hell/dunkel folgt der Windows-Einstellung.</summary>
internal static class Theme
{
    public static bool IsDark { get; private set; }

    public static void Apply()
    {
        IsDark = ReadDark();
        var r = Application.Current.Resources;
        void Set(string key, string hex) => r[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) { };
        if (IsDark)
        {
            Set("Bg", "#1C1F26"); Set("Side", "#16181E"); Set("Card", "#23272F"); Set("Fg", "#E8EBF2");
            Set("Muted", "#9AA3B5"); Set("Line", "#2E333D"); Set("Accent", "#8DB4FF"); Set("OnAccent", "#0E1117");
            Set("Pill", "#2B3A58"); Set("Success", "#3CCF7F"); Set("SuccessBg", "#14301F"); Set("Warn", "#F0B45A");
            Set("WarnBg", "#3A2A10"); Set("WarnFg", "#F5D9A8"); Set("Danger", "#F2877C"); Set("Hover", "#2A2F39");
        }
        else
        {
            Set("Bg", "#F3F4F8"); Set("Side", "#ECEEF4"); Set("Card", "#FFFFFF"); Set("Fg", "#12151C");
            Set("Muted", "#5B6272"); Set("Line", "#E3E6EE"); Set("Accent", "#1D5BD8"); Set("OnAccent", "#FFFFFF");
            Set("Pill", "#DBE6FD"); Set("Success", "#1F9D57"); Set("SuccessBg", "#E3F4EA"); Set("Warn", "#B7791F");
            Set("WarnBg", "#FFF4E0"); Set("WarnFg", "#6B4400"); Set("Danger", "#B42318"); Set("Hover", "#E7EAF1");
        }
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
    }

    public static void Watch() =>
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
                Application.Current.Dispatcher.Invoke(Apply);
        };

    private static bool ReadDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Titelleiste passend zum Thema (Windows 10 20H1+/11).</summary>
    public static void ApplyTitleBar(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) { w.SourceInitialized += (_, _) => ApplyTitleBar(w); return; }
        var dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var mica = 2; // DWMSBT_MAINWINDOW (Windows 11), sonst wirkungslos
        DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int));
    }
}
