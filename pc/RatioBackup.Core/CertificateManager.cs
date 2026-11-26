using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RatioBackup.Core;

/// <summary>Selbstsigniertes Server-Zertifikat; das Handy pinnt dessen SHA-256-Fingerabdruck.</summary>
public static class CertificateManager
{
    public static X509Certificate2 LoadOrCreate(string dir)
    {
        var file = Path.Combine(dir, "server.pfx");
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(dir);
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=RatioBackup", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
            File.WriteAllBytes(file, cert.Export(X509ContentType.Pfx));
        }
        // Immer aus der Datei laden: unter Windows braucht SslStream einen nicht-flüchtigen Schlüssel.
        return new X509Certificate2(File.ReadAllBytes(file), (string?)null, X509KeyStorageFlags.Exportable);
    }

    public static string Fingerprint(X509Certificate2 cert) =>
        Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();
}
