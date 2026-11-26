using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RatioBackup.Core;

namespace RatioBackup.Tray;

public partial class App : Application
{
    internal Backend Backend { get; private set; } = null!;
    private TrayIcon? _tray;
    private MainWindow? _main;
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private DateTime _lastQuotaWarning = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ein Fehler in der Oberfläche darf nie das ganze Programm (und damit die Sicherung) beenden.
        DispatcherUnhandledException += (_, ex) =>
        {
            ex.Handled = true;
            MessageBox.Show($"Das hat nicht geklappt:\n{ex.Exception.Message}", "RatioBackup", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        SessionEnding += (_, _) => Backend?.Dispose();
        Theme.Apply();
        Theme.Watch();

        Backend = new Backend();
        if (!Backend.IsConfigured)
        {
            var setup = new SetupWindow();
            if (setup.ShowDialog() != true) { Shutdown(); return; }
            Backend.Settings.StorageRoot = setup.Folder;
            Backend.Settings.QuotaGb = setup.QuotaGb;
            Backend.SaveSettings();
        }
        if (Environment.ProcessPath is { } exe) SelfUpdater.CleanupOld(exe);
        try { Autostart.Apply(Backend.Settings.Autostart); } catch { /* Registry gesperrt – nicht kritisch */ }

        Backend.Start();
        Backend.Changed += () => Dispatcher.BeginInvoke(OnChanged);
        Backend.QuotaWarning += s => Dispatcher.BeginInvoke(() => OnQuotaWarning(s));
        Backend.Paired += () => Dispatcher.BeginInvoke(() =>
            _tray?.Balloon("Handy gekoppelt", "Die Sicherung läuft ab jetzt jeden Abend automatisch."));
        Backend.UpdateFound += () => Dispatcher.BeginInvoke(() =>
            _tray?.Balloon("Update verfügbar", $"Version {Backend.Update?.VersionName} ist da – im Fenster unter Übersicht installieren."));

        _tray = new TrayIcon(ShowMain, () => Quit());
        _main = new MainWindow(Backend);

        if (Backend.StartError != null)
            MessageBox.Show($"RatioBackup konnte nicht vollständig starten.\n\n{Backend.StartError}", "RatioBackup", MessageBoxButton.OK, MessageBoxImage.Error);

        var startHidden = e.Args.Contains("--tray") && Backend.IsPaired;
        if (!startHidden) ShowMain();
        if (!Backend.IsPaired) _main.ShowPairing();

        SingleInstance.Listen(() => Dispatcher.BeginInvoke(ShowMain));
        _updateTimer.Tick += async (_, _) => await CheckUpdates();
        _updateTimer.Start();
        _ = CheckUpdates();
    }

    private async Task CheckUpdates()
    {
        try { await Backend.CheckForUpdateAsync(); } catch { }
        _main?.Refresh();
    }

    internal void ShowMain()
    {
        if (_main is null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void OnChanged()
    {
        var st = Backend.Store?.GetStatus();
        if (st is null) return;
        _tray?.SetTooltip(st.Run is { Active: true } r
            ? $"RatioBackup – Sicherung läuft {(r.TotalBytes > 0 ? r.DoneBytes * 100 / r.TotalBytes : 100)} %"
            : $"RatioBackup – Letzte Sicherung: {(st.LastBackupUtc is { } t ? t.ToLocalTime().ToString("dd.MM. HH:mm") : "noch keine")}");
        if (st.PendingDeletionReview) _tray?.BalloonOnce("review", "Bitte prüfen",
            "Am Handy fehlen plötzlich viele Dateien. Zur Sicherheit wurde nichts gelöscht – im Fenster unter Papierkorb entscheiden.");
    }

    private void OnQuotaWarning(double share)
    {
        if ((DateTime.Now - _lastQuotaWarning).TotalHours < 12) return;
        _lastQuotaWarning = DateTime.Now;
        _tray?.Balloon("Speicher fast voll",
            $"{share:P0} des Limits ({Backend.Settings.QuotaGb} GB) belegt. Ist es voll, wird erst der Papierkorb geleert und dann pausiert.");
    }

    internal void Quit()
    {
        _updateTimer.Stop();
        _tray?.Dispose();
        _tray = null;
        _main?.CloseForReal();
        Backend.Dispose();
        Shutdown();
    }
}
