using Microsoft.Win32;

namespace RatioBackup.Tray;

/// <summary>Start mit Windows über HKCU\...\Run (minimiert in die Taskleiste).</summary>
internal static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "RatioBackup";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true) ?? Registry.CurrentUser.CreateSubKey(Key);
        if (enabled && System.Environment.ProcessPath is { } exe)
            key.SetValue(Name, $"\"{exe}\" --tray");
        else
            key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
