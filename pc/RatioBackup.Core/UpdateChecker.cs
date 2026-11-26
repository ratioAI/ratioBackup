using System.Text.Json;

namespace RatioBackup.Core;

public record UpdateInfo(int VersionCode, string VersionName, string Notes, string Apk, string Exe);

/// <summary>Prüft version.json im Release "test" und lädt die neue EXE.</summary>
public sealed class UpdateChecker(HttpClient http, string baseUrl = UpdateChecker.DefaultBase)
{
    public const string DefaultBase = "https://github.com/ratioAI/ratioBackup/releases/download/test/";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<UpdateInfo?> CheckAsync(Version current)
    {
        try
        {
            using var res = await http.GetAsync(baseUrl + "version.json");
            if (!res.IsSuccessStatusCode) return null;
            var info = JsonSerializer.Deserialize<UpdateInfo>(await res.Content.ReadAsStringAsync(), Json);
            if (info is null || !Version.TryParse(info.VersionName + ".0", out var v)) return null;
            return v > new Version(current.Major, current.Minor, Math.Max(current.Build, 0)) ? info : null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task DownloadExeAsync(UpdateInfo info, string targetPath, IProgress<double>? progress = null)
    {
        using var res = await http.GetAsync(baseUrl + info.Exe, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        var total = res.Content.Headers.ContentLength ?? -1;
        await using var input = await res.Content.ReadAsStreamAsync();
        await using var output = File.Create(targetPath);
        var buffer = new byte[81920];
        long done = 0;
        int n;
        while ((n = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, n));
            done += n;
            if (total > 0) progress?.Report((double)done / total);
        }
    }
}

/// <summary>Tauscht die laufende EXE gegen die neue: alt → .old, neu → an ihren Platz.</summary>
public static class SelfUpdater
{
    public static void ApplyUpdate(string currentExe, string downloadedExe)
    {
        if (!File.Exists(downloadedExe)) throw new FileNotFoundException("Neue Version fehlt", downloadedExe);
        var old = currentExe + ".old";
        File.Delete(old);
        File.Move(currentExe, old);   // unter Windows auch für die laufende EXE erlaubt
        try
        {
            File.Move(downloadedExe, currentExe);
        }
        catch
        {
            File.Move(old, currentExe); // zurückrollen
            throw;
        }
    }

    public static void CleanupOld(string currentExe)
    {
        try { File.Delete(currentExe + ".old"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
