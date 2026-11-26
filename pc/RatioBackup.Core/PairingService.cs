using System.Security.Cryptography;
using System.Text;

namespace RatioBackup.Core;

/// <summary>Einmalige Kopplung per 6-stelligem Code; danach Zugriff nur mit dem ausgegebenen Token.</summary>
public sealed class PairingService
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public const int MaxAttempts = 5;

    private readonly Settings _settings;
    private readonly string _fingerprint;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();
    private string? _code;
    private DateTime _codeExpires;
    private int _failed;

    public event Action? Paired;

    public PairingService(Settings settings, string fingerprint, Func<DateTime> utcNow)
    {
        _settings = settings;
        _fingerprint = fingerprint;
        _utcNow = utcNow;
    }

    public string? CurrentCode { get { lock (_gate) return _code is not null && _utcNow() <= _codeExpires ? _code : null; } }

    public string NewCode()
    {
        lock (_gate)
        {
            _code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _codeExpires = _utcNow() + CodeLifetime;
            _failed = 0;
            return _code;
        }
    }

    public PairResult? TryPair(string code, string deviceName)
    {
        lock (_gate)
        {
            if (_code is null || _utcNow() > _codeExpires) return null;
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code ?? ""), Encoding.UTF8.GetBytes(_code)))
            {
                if (++_failed >= MaxAttempts) _code = null;
                return null;
            }
            _code = null;
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _settings.TokenSha256 = HashToken(token);
            if (!string.IsNullOrWhiteSpace(deviceName)) _settings.DeviceName = deviceName.Trim();
            var result = new PairResult(token, Proof(code!, _fingerprint));
            Paired?.Invoke();
            return result;
        }
    }

    public bool IsAuthorized(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        var expected = _settings.TokenSha256;
        if (expected is null || authorizationHeader is null || !authorizationHeader.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var actual = HashToken(authorizationHeader[prefix.Length..]);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected));
    }

    public static string Proof(string code, string fingerprint) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public record PairResult(string Token, string Proof);
