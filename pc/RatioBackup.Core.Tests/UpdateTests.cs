using System.Net;

namespace RatioBackup.Core.Tests;

public sealed class UpdateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rbu-" + Guid.NewGuid().ToString("N"));
    public UpdateTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private sealed class Fake(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public string? LastUrl;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            LastUrl = r.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    private const string Json = """{"versionCode":123,"versionName":"0.23","notes":"Neu","apk":"RatioBackup.apk","exe":"RatioBackup-PC.exe"}""";

    [Fact]
    public async Task Check_NewerVersion_ReturnsInfo()
    {
        var fake = new Fake(HttpStatusCode.OK, Json);
        var info = await new UpdateChecker(new HttpClient(fake)).CheckAsync(new Version(0, 22, 0));
        Assert.NotNull(info);
        Assert.Equal("0.23", info!.VersionName);
        Assert.Equal("https://github.com/ratioAI/ratioBackup/releases/download/test/version.json", fake.LastUrl);
    }

    [Fact]
    public async Task Check_SameVersion_Null()
        => Assert.Null(await new UpdateChecker(new HttpClient(new Fake(HttpStatusCode.OK, Json))).CheckAsync(new Version(0, 23, 0)));

    [Fact]
    public async Task Check_HttpError_ReturnsNull()
        => Assert.Null(await new UpdateChecker(new HttpClient(new Fake(HttpStatusCode.NotFound, ""))).CheckAsync(new Version(0, 1, 0)));

    [Fact]
    public async Task Check_Garbage_ReturnsNull()
        => Assert.Null(await new UpdateChecker(new HttpClient(new Fake(HttpStatusCode.OK, "<html>"))).CheckAsync(new Version(0, 1, 0)));

    [Fact]
    public void ApplyUpdate_SwapsFiles()
    {
        var cur = Path.Combine(_dir, "RatioBackup-PC.exe");
        var dl = Path.Combine(_dir, "new.exe");
        File.WriteAllText(cur, "alt");
        File.WriteAllText(dl, "neu");
        SelfUpdater.ApplyUpdate(cur, dl);
        Assert.Equal("neu", File.ReadAllText(cur));
        Assert.Equal("alt", File.ReadAllText(cur + ".old"));
        Assert.False(File.Exists(dl));
    }

    [Fact]
    public void ApplyUpdate_MissingNewFile_KeepsCurrent()
    {
        var cur = Path.Combine(_dir, "RatioBackup-PC.exe");
        File.WriteAllText(cur, "alt");
        Assert.ThrowsAny<Exception>(() => SelfUpdater.ApplyUpdate(cur, Path.Combine(_dir, "fehlt.exe")));
        Assert.Equal("alt", File.ReadAllText(cur));
    }

    [Fact]
    public void CleanupOld_DeletesOld()
    {
        var cur = Path.Combine(_dir, "RatioBackup-PC.exe");
        File.WriteAllText(cur + ".old", "x");
        SelfUpdater.CleanupOld(cur);
        Assert.False(File.Exists(cur + ".old"));
    }
}
