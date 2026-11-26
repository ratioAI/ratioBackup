using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace RatioBackup.Core.Tests;

public sealed class ApiTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rba-" + Guid.NewGuid().ToString("N"));
    private readonly DateTime _now = new(2026, 11, 26, 19, 0, 0, DateTimeKind.Utc);
    private BackupStore _store = null!;
    private PairingService _pairing = null!;
    private WebApplication _app = null!;
    private HttpClient _http = null!;
    private const long Mtime = 1764180000000;

    public async Task InitializeAsync()
    {
        _store = new BackupStore(_root, "Xiaomi 18 Pro", () => _now);
        _pairing = new PairingService(new Settings(), "abc", () => _now);
        _app = ApiServer.Build(_store, _pairing, null, configure: b => b.WebHost.UseTestServer());
        await _app.StartAsync();
        _http = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        try { Directory.Delete(_root, true); } catch { }
    }

    private async Task Authorize()
    {
        var code = _pairing.NewCode();
        var res = await _http.PostAsJsonAsync("/pair", new { code, deviceName = "Xiaomi 18 Pro" });
        res.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(PairingService.Proof(code, "abc"), body.GetProperty("proof").GetString());
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private Task<HttpResponseMessage> Put(string path, byte[] body, long size, long offset, string? sha)
    {
        var req = new HttpRequestMessage(HttpMethod.Put,
            $"/files?path={Uri.EscapeDataString(path)}&size={size}&mtimeMs={Mtime}&offset={offset}")
        { Content = new ByteArrayContent(body) };
        if (sha != null) req.Headers.Add("X-Sha256", sha);
        return _http.SendAsync(req);
    }

    [Fact]
    public async Task Pair_WrongCode_403()
    {
        _pairing.NewCode();
        var res = await _http.PostAsJsonAsync("/pair", new { code = "x", deviceName = "y" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Status_WithoutToken_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _http.GetAsync("/status")).StatusCode);

    [Fact]
    public async Task Manifest_WithoutToken_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _http.PostAsJsonAsync("/manifest", new { roots = new string[0], files = new object[0] })).StatusCode);

    [Fact]
    public async Task Manifest_ThenUpload_ThenStatusShowsUsedBytes()
    {
        await Authorize();
        var data = Encoding.UTF8.GetBytes("hallo");
        var m = await _http.PostAsJsonAsync("/manifest", new { roots = new[] { "DCIM" }, files = new[] { new { path = "DCIM/a.txt", size = 5L, mtimeMs = Mtime } } });
        var mj = JsonDocument.Parse(await m.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("DCIM/a.txt", mj.GetProperty("needed")[0].GetProperty("path").GetString());
        Assert.Equal(0, mj.GetProperty("needed")[0].GetProperty("offset").GetInt64());

        Assert.Equal(HttpStatusCode.OK, (await Put("DCIM/a.txt", data, 5, 0, Sha(data))).StatusCode);
        var st = JsonDocument.Parse(await _http.GetStringAsync("/status")).RootElement;
        Assert.Equal(5, st.GetProperty("usedBytes").GetInt64());
        Assert.False(st.GetProperty("pendingDeletionReview").GetBoolean());
    }

    [Fact]
    public async Task Upload_Partial_Returns206_ThenResume_200()
    {
        await Authorize();
        var data = Encoding.UTF8.GetBytes("0123456789");
        var first = await Put("DCIM/v.mp4", data[..4], 10, 0, null);
        Assert.Equal(HttpStatusCode.PartialContent, first.StatusCode);
        Assert.Equal(4, JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("offset").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await Put("DCIM/v.mp4", data[4..], 10, 4, Sha(data))).StatusCode);
    }

    [Fact]
    public async Task Upload_WrongOffset_409WithOffsetBody()
    {
        await Authorize();
        var res = await Put("DCIM/v.mp4", new byte[3], 10, 5, null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(0, JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("offset").GetInt64());
    }

    [Fact]
    public async Task Upload_BadSha_422()
    {
        await Authorize();
        Assert.Equal((HttpStatusCode)422, (await Put("DCIM/a.txt", new byte[5], 5, 0, Sha(new byte[] { 1 }))).StatusCode);
    }

    [Fact]
    public async Task Upload_OverQuota_507()
    {
        await Authorize();
        _store.QuotaBytes = 3;
        Assert.Equal((HttpStatusCode)507, (await Put("DCIM/a.txt", new byte[5], 5, 0, Sha(new byte[5]))).StatusCode);
    }

    [Fact]
    public async Task Upload_UnsafePath_400()
    {
        await Authorize();
        Assert.Equal(HttpStatusCode.BadRequest, (await Put("../x", new byte[1], 1, 0, Sha(new byte[1]))).StatusCode);
    }

    [Fact]
    public async Task Upload_MissingQuery_400()
    {
        await Authorize();
        var res = await _http.PutAsync("/files?path=DCIM%2Fa", new ByteArrayContent(new byte[1]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Complete_SetsLastBackup()
    {
        await Authorize();
        Assert.Equal(HttpStatusCode.OK, (await _http.PostAsync("/complete", null)).StatusCode);
        var st = JsonDocument.Parse(await _http.GetStringAsync("/status")).RootElement;
        Assert.Equal(_now, st.GetProperty("lastBackupUtc").GetDateTime().ToUniversalTime());
    }
}

public class DiscoveryTests
{
    [Fact]
    public async Task Discovery_RepliesJsonToProbe()
    {
        using var responder = new DiscoveryResponder("MEIN-PC", 47321, "abc");
        responder.Start(0);
        using var client = new UdpClient(0);
        var probe = Encoding.ASCII.GetBytes("RATIOBACKUP?");
        await client.SendAsync(probe, probe.Length, "127.0.0.1", responder.Port);
        var receive = client.ReceiveAsync();
        Assert.Same(receive, await Task.WhenAny(receive, Task.Delay(3000)));
        var json = JsonDocument.Parse(receive.Result.Buffer).RootElement;
        Assert.Equal("MEIN-PC", json.GetProperty("name").GetString());
        Assert.Equal(47321, json.GetProperty("port").GetInt32());
        Assert.Equal("abc", json.GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task Discovery_IgnoresOtherPayloads()
    {
        using var responder = new DiscoveryResponder("MEIN-PC", 47321, "abc");
        responder.Start(0);
        using var client = new UdpClient(0);
        var probe = Encoding.ASCII.GetBytes("HALLO");
        await client.SendAsync(probe, probe.Length, "127.0.0.1", responder.Port);
        var receive = client.ReceiveAsync();
        Assert.NotSame(receive, await Task.WhenAny(receive, Task.Delay(1000)));
    }
}

public sealed class HttpsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rbh-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public async Task Https_ServesWithPinnedCertificate()
    {
        var cert = CertificateManager.LoadOrCreate(_root);
        var fp = CertificateManager.Fingerprint(cert);
        var store = new BackupStore(Path.Combine(_root, "data"), "X", () => DateTime.UtcNow);
        var pairing = new PairingService(new Settings(), fp, () => DateTime.UtcNow);
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        await using var app = ApiServer.Build(store, pairing, cert, port);
        await app.StartAsync();

        string? seen = null;
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, c, _, _) =>
            {
                seen = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(c!.RawData)).ToLowerInvariant();
                return seen == fp;
            }
        };
        using var http = new HttpClient(handler);
        var res = await http.GetAsync($"https://127.0.0.1:{port}/status");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal(fp, seen);
        await app.StopAsync();
    }
}
