using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using RatioBackup.Core;

namespace RatioBackup.Tray;

/// <summary>Alles ohne Oberfläche: Einstellungen, Ablage, Kopplung, API, Gerätesuche, Updates.</summary>
internal sealed class Backend : IDisposable
{
    public const long GiB = 1024L * 1024 * 1024;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public string SettingsDir { get; } = Settings.DefaultDir;
    public Settings Settings { get; }
    public BackupStore? Store { get; private set; }
    public PairingService? Pairing { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string VerifyCode => PairingService.VerifyCode(Fingerprint);
    public string? StartError { get; private set; }
    public UpdateInfo? Update { get; private set; }

    private WebApplication? _api;
    private DiscoveryResponder? _discovery;

    public event Action? Changed;
    public event Action<double>? QuotaWarning;
    public event Action? Paired;
    public event Action? UpdateFound;

    public Backend() => Settings = Settings.Load(SettingsDir);

    public bool IsConfigured => Settings.StorageRoot is not null;
    public bool IsPaired => Settings.TokenSha256 is not null;

    public static Version CurrentVersion => typeof(Backend).Assembly.GetName().Version ?? new Version(0, 0, 0);
    public static string CurrentVersionText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}";

    public void Start()
    {
        var cert = CertificateManager.LoadOrCreate(SettingsDir);
        Fingerprint = CertificateManager.Fingerprint(cert);

        Store = new BackupStore(Settings.StorageRoot!, Settings.DeviceName, () => DateTime.UtcNow) { QuotaBytes = Settings.QuotaGb * GiB };
        Store.Changed += () => Changed?.Invoke();
        Store.QuotaWarning += s => QuotaWarning?.Invoke(s);

        Pairing = new PairingService(Settings, Fingerprint, () => DateTime.UtcNow);
        Pairing.Paired += () => { Settings.Save(SettingsDir); Paired?.Invoke(); };

        try
        {
            _api = ApiServer.Build(Store, Pairing, cert);
            // Nie auf dem UI-Thread blockierend warten: Fortsetzungen würden dort festhängen.
            Task.Run(() => _api.StartAsync()).GetAwaiter().GetResult();
            _discovery = new DiscoveryResponder(Environment.MachineName, ApiServer.DefaultPort, Fingerprint);
            _discovery.Start();
        }
        catch (Exception ex)
        {
            StartError = $"Port {ApiServer.DefaultPort} oder {DiscoveryResponder.DefaultPort} ist belegt: {ex.Message}";
        }
    }

    public void SaveSettings() => Settings.Save(SettingsDir);

    public void SetQuota(int gb)
    {
        Settings.QuotaGb = gb;
        SaveSettings();
        if (Store != null) Store.QuotaBytes = gb * GiB;
        Changed?.Invoke();
    }

    public void SetAutostart(bool on)
    {
        Settings.Autostart = on;
        SaveSettings();
        Autostart.Apply(on);
    }

    public bool IsRunActive => Store?.GetStatus().Run is { Active: true };

    public async Task CheckForUpdateAsync()
    {
        Update = await new UpdateChecker(Http).CheckAsync(CurrentVersion);
        if (Update != null) UpdateFound?.Invoke();
    }

    /// <summary>Lädt die neue EXE, tauscht sie aus und startet neu. Wirft bei Fehlern; die alte Version läuft dann weiter.</summary>
    public async Task InstallUpdateAsync(IProgress<double> progress)
    {
        if (Update is null || Environment.ProcessPath is not { } exe) return;
        var tmp = exe + ".download";
        try
        {
            await new UpdateChecker(Http).DownloadExeAsync(Update, tmp, progress);
            Store?.Flush();
            SelfUpdater.ApplyUpdate(exe, tmp);
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--after-update") { UseShellExecute = true });
    }

    public void Dispose()
    {
        _discovery?.Dispose();
        var api = _api;
        _api = null;
        if (api != null)
        {
            // Auf einem Hintergrund-Thread stoppen (sonst Deadlock mit dem UI-Thread) und höchstens 5 s warten.
            Task.Run(async () =>
            {
                try { using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3)); await api.StopAsync(cts.Token); } catch { }
                try { await api.DisposeAsync(); } catch { }
            }).Wait(TimeSpan.FromSeconds(5));
        }
        Store?.Flush();
    }
}
