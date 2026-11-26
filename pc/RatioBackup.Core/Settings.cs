using System.Text.Json;

namespace RatioBackup.Core;

/// <summary>Einstellungen des PC-Programms, gespeichert als settings.json.</summary>
public sealed class Settings
{
    public string? StorageRoot { get; set; }
    public int QuotaGb { get; set; } = 100;
    public string DeviceName { get; set; } = "Xiaomi 18 Pro";
    public string? TokenSha256 { get; set; }
    public bool Autostart { get; set; } = true;

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RatioBackup");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load(string dir)
    {
        var file = Path.Combine(dir, "settings.json");
        if (!File.Exists(file)) return new Settings();
        return JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), Json) ?? new Settings();
    }

    public void Save(string dir)
    {
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "settings.json");
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(this, Json));
        File.Move(file + ".tmp", file, overwrite: true);
    }
}
