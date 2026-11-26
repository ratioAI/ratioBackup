namespace RatioBackup.Core.Tests;

public class PairingTests
{
    private DateTime _now = new(2026, 11, 26, 19, 0, 0, DateTimeKind.Utc);
    private readonly Settings _settings = new();
    private PairingService New(string fp = "abc") => new(_settings, fp, () => _now);

    [Fact]
    public void VerifyCode_first4Upper() => Assert.Equal("4F2A", PairingService.VerifyCode("4f2a9c00ff"));

    [Fact]
    public void NewCode_IsSixDigits()
    {
        var code = New().NewCode();
        Assert.Matches("^[0-9]{6}$", code);
    }

    [Fact]
    public void Proof_IsHmacOfFingerprintKeyedWithCode()
        => Assert.Equal("f6ced6f4883ffc0981a6b9945819f680102b43097ad8ef7a0df9bde06fb3d2e4", PairingService.Proof("123456", "abc"));

    [Fact]
    public void TryPair_CorrectCode_ReturnsTokenAndProof()
    {
        var p = New();
        var code = p.NewCode();
        var r = p.TryPair(code, "Xiaomi 18 Pro");
        Assert.NotNull(r);
        Assert.Equal(PairingService.Proof(code, "abc"), r!.Proof);
        Assert.Equal(32, Convert.FromBase64String(r.Token).Length);
        Assert.Equal("Xiaomi 18 Pro", _settings.DeviceName);
    }

    [Fact]
    public void TryPair_AfterFiveMinutes_Null()
    {
        var p = New();
        var code = p.NewCode();
        _now = _now.AddMinutes(5).AddSeconds(1);
        Assert.Null(p.TryPair(code, "x"));
    }

    [Fact]
    public void TryPair_AfterFiveWrong_NullEvenIfCorrect()
    {
        var p = New();
        var code = p.NewCode();
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++) Assert.Null(p.TryPair(wrong, "x"));
        Assert.Null(p.TryPair(code, "x"));
    }

    [Fact]
    public void Lockout_SurvivesNewCode_ForOneMinute()
    {
        var p = New();
        p.NewCode();
        for (var i = 0; i < 5; i++) p.TryPair("x", "x");
        var code = p.NewCode();
        Assert.Null(p.TryPair(code, "x"));
        _now = _now.AddMinutes(1).AddSeconds(1);
        code = p.NewCode();
        Assert.NotNull(p.TryPair(code, "x"));
    }

    [Fact]
    public void Code_IsSingleUse()
    {
        var p = New();
        var code = p.NewCode();
        Assert.NotNull(p.TryPair(code, "x"));
        Assert.Null(p.TryPair(code, "x"));
    }

    [Fact]
    public void WithoutCode_PairFails() => Assert.Null(New().TryPair("123456", "x"));

    [Fact]
    public void IsAuthorized_OnlyIssuedToken()
    {
        var p = New();
        var token = p.TryPair(p.NewCode(), "x")!.Token;
        Assert.True(p.IsAuthorized("Bearer " + token));
        Assert.False(p.IsAuthorized("Bearer " + Convert.ToBase64String(new byte[32])));
        Assert.False(p.IsAuthorized(null));
        Assert.False(p.IsAuthorized(token));
        Assert.DoesNotContain(token, _settings.TokenSha256 ?? "");
    }

    [Fact]
    public void NewPairing_InvalidatesOldToken()
    {
        var p = New();
        var t1 = p.TryPair(p.NewCode(), "x")!.Token;
        var t2 = p.TryPair(p.NewCode(), "x")!.Token;
        Assert.False(p.IsAuthorized("Bearer " + t1));
        Assert.True(p.IsAuthorized("Bearer " + t2));
    }

    [Fact]
    public void Paired_EventFires()
    {
        var p = New();
        var fired = false;
        p.Paired += () => fired = true;
        p.TryPair(p.NewCode(), "x");
        Assert.True(fired);
    }
}

public sealed class CertificateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rbc-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Certificate_PersistsSameFingerprint()
    {
        var a = CertificateManager.Fingerprint(CertificateManager.LoadOrCreate(_dir));
        var b = CertificateManager.Fingerprint(CertificateManager.LoadOrCreate(_dir));
        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void Certificate_HasPrivateKey() => Assert.True(CertificateManager.LoadOrCreate(_dir).HasPrivateKey);
}

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rbs-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Defaults()
    {
        var s = Settings.Load(_dir);
        Assert.Null(s.StorageRoot);
        Assert.Equal(100, s.QuotaGb);
        Assert.Equal("Xiaomi 18 Pro", s.DeviceName);
        Assert.True(s.Autostart);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var s = new Settings { StorageRoot = @"D:\Handy", QuotaGb = 50, DeviceName = "X", TokenSha256 = "ab", Autostart = false };
        s.Save(_dir);
        var l = Settings.Load(_dir);
        Assert.Equal(@"D:\Handy", l.StorageRoot);
        Assert.Equal(50, l.QuotaGb);
        Assert.Equal("X", l.DeviceName);
        Assert.Equal("ab", l.TokenSha256);
        Assert.False(l.Autostart);
    }
}
