using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RatioBackup.Core;

namespace RatioBackup.Tray;

public partial class MainWindow : Window
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private readonly Backend _b;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly HashSet<string> _selected = new();
    private bool _reallyClose;
    private long _lastBytes = -1;
    private DateTime _lastTick;
    private double _speed;

    internal MainWindow(Backend backend)
    {
        InitializeComponent();
        Theme.ApplyTitleBar(this);
        _b = backend;
        _timer.Tick += (_, _) => { if (IsVisible) RefreshLive(); };
        _timer.Start();
        _b.Changed += () => Dispatcher.BeginInvoke(() => { if (IsVisible) RefreshLists(); });
        _b.Paired += () => Dispatcher.BeginInvoke(Refresh);
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); };
    }

    /// <summary>Schließen = in die Taskleiste; Beenden geht über das Symbol.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }

    internal void CloseForReal()
    {
        _reallyClose = true;
        _timer.Stop();
        Close();
    }

    internal void ShowPairing()
    {
        if (_b.Pairing is null) return;
        var w = new PairingWindow(_b.Pairing, _b.VerifyCode);
        if (IsVisible) w.Owner = this;
        _b.Paired += Close;
        w.Closed += (_, _) => _b.Paired -= Close;
        w.Show();
        void Close() => Dispatcher.BeginInvoke(w.Close);
    }

    internal void Refresh()
    {
        RefreshLive();
        RefreshLists();
        RefreshSettings();
    }

    // ---------- Übersicht ----------

    private void RefreshLive()
    {
        TodayText.Text = DateTime.Now.ToString("dddd, d. MMMM", De);
        SideDevice.Text = _b.IsPaired ? _b.Settings.DeviceName : "Noch kein Handy";
        SideVerify.Text = _b.IsPaired ? $"Prüfcode {_b.VerifyCode}" : "Einstellungen → Handy koppeln";

        if (_b.StartError != null) { ErrorBanner.Visibility = Visibility.Visible; ErrorText.Text = _b.StartError; }
        else ErrorBanner.Visibility = Visibility.Collapsed;

        UpdateBanner.Visibility = _b.Update != null ? Visibility.Visible : Visibility.Collapsed;
        if (_b.Update != null && UpdateButton.IsEnabled) UpdateText.Text = $"Version {_b.Update.VersionName} verfügbar";

        var st = _b.Store?.GetStatus();
        if (st is null) return;
        TrashBadge.Visibility = st.PendingDeletionReview ? Visibility.Visible : Visibility.Collapsed;

        var run = st.Run;
        if (run is { Active: true })
        {
            var now = DateTime.UtcNow;
            if (_lastBytes >= 0 && run.DoneBytes >= _lastBytes)
            {
                var dt = (now - _lastTick).TotalSeconds;
                if (dt > 0) _speed = _speed <= 0 ? (run.DoneBytes - _lastBytes) / dt : _speed * 0.7 + (run.DoneBytes - _lastBytes) / dt * 0.3;
            }
            _lastBytes = run.DoneBytes;
            _lastTick = now;
            var share = run.TotalBytes > 0 ? (double)run.DoneBytes / run.TotalBytes : 1;
            RunRing.Value = share;
            RunPercent.Text = $"{(int)(share * 100)} %";
            var remaining = run.TotalBytes - run.DoneBytes;
            RunSub.Text = _speed > 1 ? $"noch ca. {Duration(remaining / _speed)}" : "läuft …";
            RunKicker.Text = $"SICHERUNG LÄUFT · SEIT {run.StartedUtc.ToLocalTime():HH:mm}";
            RunStats.Visibility = Visibility.Visible;
            IdleText.Visibility = Visibility.Collapsed;
            StatFiles.Text = $"{run.DoneFiles:N0} / {run.TotalFiles:N0}".Replace(',', '.');
            StatBytes.Text = $"{Bytes(run.DoneBytes)} / {Bytes(run.TotalBytes)}";
            StatSpeed.Text = _speed > 0 ? $"{Bytes((long)_speed)}/s" : "–";
        }
        else
        {
            _lastBytes = -1;
            _speed = 0;
            RunRing.Value = st.LastBackupUtc != null ? 1 : 0;
            RunPercent.Text = st.LastBackupUtc != null ? "✓" : "–";
            RunSub.Text = st.LastBackupUtc != null ? "aktuell" : "";
            RunKicker.Text = "BEREIT";
            RunStats.Visibility = Visibility.Collapsed;
            IdleText.Visibility = Visibility.Visible;
            IdleText.Text = st.LastBackupUtc is { } t
                ? $"Letzte Sicherung {Friendly(t.ToLocalTime())}. Das Handy meldet sich zur eingestellten Uhrzeit (Standard 20:00)."
                : "Noch keine Sicherung. Tippe am Handy auf „Jetzt sichern“ oder warte bis zur eingestellten Uhrzeit.";
        }

        var mirror = Math.Max(0, st.UsedBytes - st.TrashBytes);
        var q = Math.Max(1, st.QuotaBytes);
        StorageRing.Value = (double)mirror / q;
        StorageRing.Value2 = (double)st.TrashBytes / q;
        StorageUsed.Text = Bytes(st.UsedBytes);
        StorageQuota.Text = $"von {Bytes(st.QuotaBytes)} Limit";
        StorageTrash.Text = $"Papierkorb {Bytes(st.TrashBytes)}";
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_b.Settings.StorageRoot!)!);
            StorageDisk.Text = $"Laufwerk {drive.Name.TrimEnd('\\')} {Bytes(drive.AvailableFreeSpace)} frei";
        }
        catch { StorageDisk.Text = ""; }
        CardLast.Text = st.LastBackupUtc is { } last ? Friendly(last.ToLocalTime()) : "noch keine";
        CardMirror.Text = Bytes(mirror);
    }

    private void RefreshLists()
    {
        if (_b.Store is null) return;
        var history = _b.Store.History;
        RecentRuns.Children.Clear();
        foreach (var r in history.Take(3)) RecentRuns.Children.Add(RunRow(r));
        if (history.Count == 0) RecentRuns.Children.Add(Muted("Noch keine Läufe."));
        HistoryList.Children.Clear();
        foreach (var r in history) HistoryList.Children.Add(RunRow(r));
        if (history.Count == 0) HistoryList.Children.Add(Muted("Noch keine Läufe. Die erste Sicherung erscheint hier."));
        RefreshTrash();
    }

    private UIElement RunRow(RunRecord r)
    {
        var (glyph, brush, title) = r.Result switch
        {
            "ok" => ("", "Success", r.Files == 0 ? "Alles aktuell" : "Erfolgreich gesichert"),
            "interrupted" => ("", "Warn", "Unterbrochen – geht beim nächsten Mal weiter"),
            _ => ("", "Warn", r.Result),
        };
        var g = new Grid { Margin = new Thickness(0, 9, 0, 9) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        var icon = new TextBlock { Text = glyph, FontFamily = (FontFamily)FindResource("Icons"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(TextBlock.ForegroundProperty, brush);
        g.Children.Add(icon);
        var t = new TextBlock { Text = title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 1); g.Children.Add(t);
        var dur = r.EndUtc - r.StartUtc;
        var info = Muted($"{r.Files:N0} Dateien · {Bytes(r.Bytes)} · {Duration(dur.TotalSeconds)}".Replace(',', '.'));
        Grid.SetColumn(info, 2); g.Children.Add(info);
        var when = Muted(Friendly(r.StartUtc.ToLocalTime()));
        when.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(when, 3); g.Children.Add(when);
        var wrap = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = g };
        wrap.SetResourceReference(Border.BorderBrushProperty, "Line");
        return wrap;
    }

    // ---------- Papierkorb ----------

    private void RefreshTrash()
    {
        if (_b.Store is null) return;
        var st = _b.Store.GetStatus();
        var held = _b.Store.HeldBackDeletions;
        ReviewBanner.Visibility = st.PendingDeletionReview && held.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReviewText.Text = $"Am Handy fehlen plötzlich {held.Count:N0} Dateien (z. B. {string.Join(", ", held.Take(2).Select(p => p[(p.LastIndexOf('/') + 1)..]))}). Zur Sicherheit wurde nichts gelöscht.".Replace(',', ',');

        var all = _b.Store.ListTrash();
        _selected.IntersectWith(all.Select(e => e.Id));
        var q = TrashSearch.Text.Trim();
        var shown = q.Length == 0 ? all : all.Where(e => e.PhonePath.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        TrashSummary.Text = $"{(_selected.Count > 0 ? $"{_selected.Count} ausgewählt · " : "")}{all.Count:N0} Dateien · {Bytes(all.Sum(e => e.Size))} im Papierkorb";
        RestoreSelected.IsEnabled = DeleteSelected.IsEnabled = _selected.Count > 0;

        TrashList.Children.Clear();
        if (shown.Count == 0) { TrashList.Children.Add(Muted(all.Count == 0 ? "Der Papierkorb ist leer." : "Keine Treffer.")); return; }
        foreach (var day in shown.GroupBy(e => e.Day))
        {
            var header = new TextBlock { Text = DayTitle(day.Key).ToUpper(De), FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 6) };
            header.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            TrashList.Children.Add(header);
            foreach (var e in day) TrashList.Children.Add(TrashRow(e));
        }
    }

    private UIElement TrashRow(TrashEntry e)
    {
        var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        foreach (var w in new[] { 32.0, -1, 110, 130, 150 })
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
        var cb = new CheckBox { IsChecked = _selected.Contains(e.Id), ToolTip = "Auswählen" };
        cb.Click += (_, _) => { if (cb.IsChecked == true) _selected.Add(e.Id); else _selected.Remove(e.Id); RefreshTrash(); };
        g.Children.Add(cb);
        var name = new StackPanel();
        var slash = e.PhonePath.LastIndexOf('/');
        name.Children.Add(new TextBlock { Text = e.PhonePath[(slash + 1)..], FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        name.Children.Add(Muted(slash > 0 ? e.PhonePath[..slash] : ""));
        Grid.SetColumn(name, 1); g.Children.Add(name);
        var size = Muted(Bytes(e.Size)); Grid.SetColumn(size, 2); g.Children.Add(size);
        var left = BackupStore.TrashDays - (int)(DateTime.UtcNow.Date - DateTime.ParseExact(e.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture)).TotalDays;
        var leftText = Muted($"noch {Math.Max(0, left)} Tage"); Grid.SetColumn(leftText, 3); g.Children.Add(leftText);
        var btn = new Button { Content = "Wiederherstellen", Height = 30, HorizontalAlignment = HorizontalAlignment.Right };
        btn.SetResourceReference(StyleProperty, "Secondary");
        btn.Click += (_, _) => { _b.Store?.Restore(e.Id); _selected.Remove(e.Id); RefreshTrash(); };
        Grid.SetColumn(btn, 4); g.Children.Add(btn);
        var wrap = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = g };
        wrap.SetResourceReference(Border.BorderBrushProperty, "Line");
        return wrap;
    }

    private void TrashSearch_Changed(object sender, TextChangedEventArgs e) => RefreshTrash();

    private void RestoreSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var id in _selected.ToList()) _b.Store?.Restore(id);
        _selected.Clear();
        RefreshTrash();
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"{_selected.Count} Dateien endgültig löschen? Das lässt sich nicht rückgängig machen.", "RatioBackup",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _b.Store?.DeletePermanently(_selected.ToList());
        _selected.Clear();
        RefreshTrash();
    }

    private void KeepHeld_Click(object sender, RoutedEventArgs e) { _b.Store?.DiscardHeldBackDeletions(); RefreshTrash(); }
    private void TrashHeld_Click(object sender, RoutedEventArgs e) { _b.Store?.ConfirmHeldBackDeletions(); RefreshTrash(); }

    // ---------- Einstellungen ----------

    private void RefreshSettings()
    {
        SettingsVersion.Text = $"RatioBackup {Backend.CurrentVersionText}";
        SetFolder.Text = _b.Settings.StorageRoot ?? "";
        SetQuota.Text = _b.Settings.QuotaGb.ToString(CultureInfo.InvariantCulture);
        SetAutostart.IsChecked = _b.Settings.Autostart;
        SetDevice.Text = _b.IsPaired ? $"{_b.Settings.DeviceName} · Prüfcode {_b.VerifyCode}" : "Noch kein Handy gekoppelt";
        SetUpdate.Text = _b.Update != null ? $"Version {_b.Update.VersionName} verfügbar (installiert: {Backend.CurrentVersionText})" : $"Version {Backend.CurrentVersionText}";
        CheckUpdateButton.Content = _b.Update != null ? "Jetzt aktualisieren" : "Nach Updates suchen";
    }

    private void SaveQuota_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(SetQuota.Text.Trim(), out var gb) && gb >= 5 && gb <= 4000) { _b.SetQuota(gb); QuotaSaved.Text = "Gespeichert"; }
        else QuotaSaved.Text = "Bitte 5 bis 4000 eingeben";
    }

    private void Autostart_Click(object sender, RoutedEventArgs e) => _b.SetAutostart(SetAutostart.IsChecked == true);

    private void Pair_Click(object sender, RoutedEventArgs e) => ShowPairing();

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_b.Update != null) { Update_Click(sender, e); return; }
        SetUpdate.Text = "Suche …";
        await _b.CheckForUpdateAsync();
        RefreshSettings();
        if (_b.Update == null) SetUpdate.Text = $"Version {Backend.CurrentVersionText} · aktuell";
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_b.IsRunActive)
        {
            MessageBox.Show(this, "Gerade läuft eine Sicherung. Das Update geht, sobald sie fertig ist.", "RatioBackup");
            return;
        }
        UpdateButton.IsEnabled = false;
        try
        {
            await _b.InstallUpdateAsync(new Progress<double>(p => UpdateText.Text = $"Update wird geladen … {p:P0}"));
            ((App)Application.Current).Quit();
        }
        catch (Exception ex)
        {
            UpdateButton.IsEnabled = true;
            MessageBox.Show(this, $"Update fehlgeschlagen: {ex.Message}\nDie bisherige Version läuft weiter.", "RatioBackup",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------- Navigation & Hilfen ----------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageOverview is null) return; // während InitializeComponent
        PageOverview.Visibility = NavOverview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageTrash.Visibility = NavTrash.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageHistory.Visibility = NavHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (_b != null) Refresh();
    }

    private void ShowHistory_Click(object sender, RoutedEventArgs e) => NavHistory.IsChecked = true;

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_b.Settings.StorageRoot is { } dir && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    private TextBlock Muted(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        return t;
    }

    internal static string Bytes(long b) => b switch
    {
        >= 1L << 30 => (b / (double)(1L << 30)).ToString("0.0", De) + " GB",
        >= 1L << 20 => (b / (double)(1L << 20)).ToString("0", De) + " MB",
        >= 1L << 10 => (b / 1024.0).ToString("0", De) + " KB",
        _ => $"{b} B",
    };

    private static string Duration(double sec) => sec switch
    {
        >= 3600 => $"{(int)(sec / 3600)} Std {(int)(sec % 3600 / 60)} Min",
        >= 60 => $"{(int)(sec / 60)} Min",
        _ => "< 1 Min",
    };

    private static string Friendly(DateTime local)
    {
        var d = local.Date;
        var day = d == DateTime.Today ? "Heute" : d == DateTime.Today.AddDays(-1) ? "Gestern" : local.ToString("ddd dd.MM.", De);
        return $"{day}, {local:HH:mm}";
    }

    private static string DayTitle(string day)
    {
        var d = DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return d == DateTime.UtcNow.Date ? "Heute" : d.ToString("dddd, d. MMMM", De);
    }
}
