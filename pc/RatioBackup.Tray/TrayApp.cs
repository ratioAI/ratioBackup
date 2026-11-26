using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.AspNetCore.Builder;
using RatioBackup.Core;

namespace RatioBackup.Tray;

/// <summary>Taskleistensymbol: startet API und Gerätesuche, zeigt Status und Warnungen.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const long GiB = 1024L * 1024 * 1024;

    private readonly string _settingsDir = Settings.DefaultDir;
    private readonly Settings _settings;
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _autostartItem;
    private BackupStore? _store;
    private PairingService? _pairing;
    private WebApplication? _api;
    private DiscoveryResponder? _discovery;
    private PairingForm? _pairingForm;
    private DateTime _lastQuotaWarning = DateTime.MinValue;
    private bool _deletionPromptOpen;
    private UpdateInfo? _update;
    private readonly ToolStripMenuItem _updateItem = new("Nach Updates suchen");
    private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private DateTime _lastUpload = DateTime.MinValue;

    public TrayApp()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = Settings.Load(_settingsDir);

        _autostartItem = new ToolStripMenuItem("Mit Windows starten") { CheckOnClick = true, Checked = _settings.Autostart };
        _autostartItem.CheckedChanged += (_, _) =>
        {
            _settings.Autostart = _autostartItem.Checked;
            _settings.Save(_settingsDir);
            Autostart.Apply(_settings.Autostart);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Status anzeigen", null, (_, _) => ShowStatus());
        menu.Items.Add("Ordner öffnen", null, (_, _) => OpenFolder());
        menu.Items.Add("Handy neu koppeln", null, (_, _) => ShowPairing());
        menu.Items.Add("Speicherlimit ändern…", null, (_, _) => ChangeQuota());
        menu.Items.Add(_autostartItem);
        _updateItem.Click += async (_, _) => { if (_update != null) await InstallUpdate(); else await CheckForUpdate(manual: true); };
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());

        _icon = new NotifyIcon { Icon = CreateIcon(), Text = "RatioBackup", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => ShowStatus();
        _icon.BalloonTipClicked += (_, _) => { if (_store?.GetStatus().PendingDeletionReview == true) ReviewDeletions(); };

        if (_settings.StorageRoot is null && !RunSetup())
        {
            _icon.Visible = false;
            Environment.Exit(0);
            return;
        }

        Autostart.Apply(_settings.Autostart);
        if (Environment.ProcessPath is { } exe) SelfUpdater.CleanupOld(exe);
        Start();
        _updateTimer.Tick += async (_, _) => await CheckForUpdate(manual: false);
        _updateTimer.Start();
        _ = CheckForUpdate(manual: false);
    }

    private static Version CurrentVersion =>
        typeof(TrayApp).Assembly.GetName().Version ?? new Version(0, 0, 0);

    private async Task CheckForUpdate(bool manual)
    {
        _update = await new UpdateChecker(Http).CheckAsync(CurrentVersion);
        if (_update != null)
        {
            _updateItem.Text = $"Update {_update.VersionName} installieren";
            _icon.ShowBalloonTip(8000, "RatioBackup – Update verfügbar",
                $"Version {_update.VersionName} ist da. Rechtsklick aufs Symbol → „Update installieren“.", ToolTipIcon.Info);
        }
        else if (manual)
        {
            MessageBox.Show($"Du hast die neueste Version ({CurrentVersion.Major}.{CurrentVersion.Minor}).", "RatioBackup");
        }
    }

    private async Task InstallUpdate()
    {
        if (_update is null || Environment.ProcessPath is not { } exe) return;
        if (DateTime.UtcNow - _lastUpload < TimeSpan.FromMinutes(2))
        {
            MessageBox.Show("Gerade läuft eine Sicherung. Das Update geht, sobald sie fertig ist.", "RatioBackup");
            return;
        }
        var tmp = exe + ".download";
        try
        {
            _updateItem.Enabled = false;
            _updateItem.Text = "Update wird geladen …";
            await new UpdateChecker(Http).DownloadExeAsync(_update, tmp,
                new Progress<double>(p => _updateItem.Text = $"Update wird geladen … {p:P0}"));
            _store?.Flush();
            SelfUpdater.ApplyUpdate(exe, tmp);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--after-update") { UseShellExecute = true });
            ExitThread();
        }
        catch (Exception ex)
        {
            try { File.Delete(tmp); } catch { }
            _updateItem.Enabled = true;
            _updateItem.Text = $"Update {_update.VersionName} installieren";
            MessageBox.Show($"Update fehlgeschlagen: {ex.Message}\nDie bisherige Version läuft weiter.", "RatioBackup",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool RunSetup()
    {
        using var form = new SetupForm();
        if (form.ShowDialog() != DialogResult.OK || form.Folder.Length == 0) return false;
        Directory.CreateDirectory(form.Folder);
        _settings.StorageRoot = form.Folder;
        _settings.QuotaGb = form.QuotaGb;
        _settings.Save(_settingsDir);
        return true;
    }

    private void Start()
    {
        var cert = CertificateManager.LoadOrCreate(_settingsDir);
        var fingerprint = CertificateManager.Fingerprint(cert);

        _store = new BackupStore(_settings.StorageRoot!, _settings.DeviceName, () => DateTime.UtcNow)
        {
            QuotaBytes = _settings.QuotaGb * GiB,
        };
        _store.Changed += () => _ui.Post(_ => OnStoreChanged(), null);
        _store.QuotaWarning += share => _ui.Post(_ => OnQuotaWarning(share), null);

        _pairing = new PairingService(_settings, fingerprint, () => DateTime.UtcNow);
        _pairing.Paired += () => _ui.Post(_ => OnPaired(), null);

        try
        {
            _api = ApiServer.Build(_store, _pairing, cert);
            _api.StartAsync().GetAwaiter().GetResult();
            _discovery = new DiscoveryResponder(Environment.MachineName, ApiServer.DefaultPort, fingerprint);
            _discovery.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"RatioBackup konnte nicht starten (Port {ApiServer.DefaultPort} oder {DiscoveryResponder.DefaultPort} belegt?).\n\n{ex.Message}",
                "RatioBackup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        OnStoreChanged();
        if (_settings.TokenSha256 is null) ShowPairing();
    }

    private void OnPaired()
    {
        _settings.Save(_settingsDir);
        _pairingForm?.Close();
        _icon.ShowBalloonTip(5000, "RatioBackup", "Handy gekoppelt. Die Sicherung läuft ab jetzt jeden Abend ab 20 Uhr.", ToolTipIcon.Info);
    }

    private void OnStoreChanged()
    {
        if (_store is null) return;
        _lastUpload = DateTime.UtcNow;
        var st = _store.GetStatus();
        var last = st.LastBackupUtc is { } t ? t.ToLocalTime().ToString("dd.MM. HH:mm") : "noch keine";
        var text = $"RatioBackup – Letzte Sicherung: {last}";
        _icon.Text = text.Length > 63 ? text[..63] : text;

        if (st.PendingDeletionReview && !_deletionPromptOpen)
            _icon.ShowBalloonTip(10000, "RatioBackup – bitte prüfen",
                $"Am Handy fehlen plötzlich {_store.HeldBackDeletions.Count} Dateien. Zur Sicherheit wurde nichts gelöscht. Hier klicken zum Prüfen.",
                ToolTipIcon.Warning);
    }

    private void OnQuotaWarning(double share)
    {
        if ((DateTime.Now - _lastQuotaWarning).TotalHours < 12) return;
        _lastQuotaWarning = DateTime.Now;
        _icon.ShowBalloonTip(8000, "RatioBackup – Speicher fast voll",
            $"{share:P0} des Speicherlimits ({_settings.QuotaGb} GB) belegt. Ist es voll, wird erst der Papierkorb geleert und dann die Sicherung pausiert.",
            ToolTipIcon.Warning);
    }

    private void ReviewDeletions()
    {
        if (_store is null || _deletionPromptOpen) return;
        var list = _store.HeldBackDeletions;
        if (list.Count == 0) return;
        _deletionPromptOpen = true;
        try
        {
            var sample = string.Join("\n", list.Take(10)) + (list.Count > 10 ? $"\n… und {list.Count - 10} weitere" : "");
            var answer = MessageBox.Show(
                $"Am Handy fehlen {list.Count} Dateien, die gesichert sind:\n\n{sample}\n\n" +
                "Ja = in den Papierkorb verschieben (30 Tage wiederherstellbar)\nNein = behalten",
                "RatioBackup – Dateien gelöscht?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes) _store.ConfirmHeldBackDeletions();
            else _store.DiscardHeldBackDeletions();
        }
        finally
        {
            _deletionPromptOpen = false;
        }
    }

    private void ShowStatus()
    {
        if (_store is null) return;
        var st = _store.GetStatus();
        var last = st.LastBackupUtc is { } t ? t.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "noch keine";
        var msg = $"Letzte Sicherung: {last}\n" +
                  $"Belegt: {st.UsedBytes / (double)GiB:0.0} von {st.QuotaBytes / (double)GiB:0} GB\n" +
                  $"davon Papierkorb: {st.TrashBytes / (double)GiB:0.0} GB\n" +
                  $"Ordner: {_settings.StorageRoot}\n" +
                  $"Gekoppelt: {(_settings.TokenSha256 is null ? "nein" : _settings.DeviceName)}";
        if (st.PendingDeletionReview)
        {
            msg += "\n\nEs gibt zurückgehaltene Löschungen. Jetzt prüfen?";
            if (MessageBox.Show(msg, "RatioBackup", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                ReviewDeletions();
            return;
        }
        MessageBox.Show(msg, "RatioBackup", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenFolder()
    {
        if (_settings.StorageRoot is { } dir && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    private void ShowPairing()
    {
        if (_pairing is null) return;
        if (_pairingForm is { IsDisposed: false }) { _pairingForm.Activate(); return; }
        _pairingForm = new PairingForm(_pairing);
        _pairingForm.FormClosed += (_, _) => _pairingForm = null;
        _pairingForm.Show();
    }

    private void ChangeQuota()
    {
        using var form = new Form
        {
            Text = "Speicherlimit", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16),
        };
        var input = new NumericUpDown { Minimum = 5, Maximum = 4000, Value = Math.Clamp(_settings.QuotaGb, 5, 4000), Width = 80 };
        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        row.Controls.Add(new Label { Text = "Speicherlimit (GB):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        row.Controls.Add(input);
        row.Controls.Add(ok);
        form.Controls.Add(row);
        form.AcceptButton = ok;
        if (form.ShowDialog() != DialogResult.OK) return;
        _settings.QuotaGb = (int)input.Value;
        _settings.Save(_settingsDir);
        if (_store != null) _store.QuotaBytes = _settings.QuotaGb * GiB;
    }

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, "R", font, new Rectangle(0, 0, 32, 32), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _discovery?.Dispose();
        if (_api != null)
        {
            _api.StopAsync().GetAwaiter().GetResult();
            _api.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _store?.Flush();
        base.ExitThreadCore();
    }
}
