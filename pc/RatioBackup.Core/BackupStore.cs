using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RatioBackup.Core;

/// <summary>
/// Spiegel des Handys auf dem PC: Index, Abgleich, Uploads mit Wiederaufnahme,
/// Papierkorb (30 Tage), Speicherlimit und Schutz vor Massenlöschung.
/// </summary>
public sealed class BackupStore
{
    public const int TrashDays = 30;
    public const int MassDeletionFloor = 50;
    public const double MassDeletionShare = 0.2;
    public const double WarnShare = 0.9;
    private const string TrashFolder = "_Papierkorb";

    private sealed record IndexEntry(string Stored, long Size, long MtimeMs);
    private sealed record PartMeta(long Size, long MtimeMs);

    private sealed class State
    {
        public Dictionary<string, IndexEntry> Index { get; set; } = new();
        public DateTime? LastBackupUtc { get; set; }
        public List<string> HeldBack { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _root;
    private readonly string _deviceDir;
    private readonly string _metaDir;
    private readonly string _partsDir;
    private readonly string _trashDir;
    private readonly string _statePath;
    private readonly Func<DateTime> _utcNow;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly State _state;

    public long QuotaBytes { get; set; } = 100L * 1024 * 1024 * 1024;
    public event Action<double>? QuotaWarning;
    public event Action? Changed;

    public BackupStore(string root, string deviceName, Func<DateTime> utcNow)
    {
        _root = root;
        _deviceDir = Path.Combine(root, deviceName);
        _metaDir = Path.Combine(root, ".ratiobackup");
        _partsDir = Path.Combine(_metaDir, "parts");
        _trashDir = Path.Combine(root, TrashFolder);
        _statePath = Path.Combine(_metaDir, "index.json");
        _utcNow = utcNow;
        Directory.CreateDirectory(_partsDir);
        Directory.CreateDirectory(_deviceDir);
        _state = File.Exists(_statePath)
            ? JsonSerializer.Deserialize<State>(File.ReadAllText(_statePath), Json) ?? new State()
            : new State();
    }

    public IReadOnlyList<string> HeldBackDeletions => WithLock(() => _state.HeldBack.ToList());

    public ManifestResult ApplyManifest(ManifestRequest m) => WithLock(() =>
    {
        var onPhone = new Dictionary<string, FileEntry>();
        foreach (var f in m.Files)
            if (PathMapper.IsSafe(f.Path)) onPhone[f.Path] = f;

        var needed = new List<NeededFile>();
        foreach (var f in onPhone.Values)
        {
            if (_state.Index.TryGetValue(f.Path, out var e) && e.Size == f.Size && e.MtimeMs == f.MtimeMs
                && File.Exists(Path.Combine(_deviceDir, Native(e.Stored))))
                continue;
            needed.Add(new NeededFile(f.Path, PartOffset(f.Path, f.Size, f.MtimeMs)));
        }

        var trashed = 0;
        var held = new List<string>();
        foreach (var root in m.Roots.Where(PathMapper.IsSafe).Distinct())
        {
            var known = _state.Index.Keys.Where(k => UnderRoot(k, root)).ToList();
            var missing = known.Where(k => !onPhone.ContainsKey(k)).ToList();
            if (missing.Count == 0) continue;
            if (missing.Count >= MassDeletionFloor && (double)missing.Count / known.Count > MassDeletionShare)
            {
                held.AddRange(missing);
                continue;
            }
            foreach (var k in missing) { MoveToTrash(k); trashed++; }
        }

        _state.HeldBack = held;
        Save();
        Changed?.Invoke();
        return new ManifestResult(needed, trashed, held.Count);
    });

    public async Task<UploadResult> WriteAsync(string path, long size, long mtimeMs, long offset, Stream body, string? sha256Hex)
    {
        if (!PathMapper.IsSafe(path)) throw new ArgumentException($"Unzulässiger Pfad: {path}", nameof(path));
        if (size < 0 || offset < 0) throw new ArgumentException("Ungültige Größe oder Offset");

        await _lock.WaitAsync();
        try
        {
            var part = PartPath(path);
            var current = PartOffset(path, size, mtimeMs);
            if (offset != current) return new UploadResult(UploadOutcome.WrongOffset, current);

            var incoming = size - offset;
            if (UsedBytes() + incoming > QuotaBytes)
            {
                PurgeTrash(all: true);
                if (UsedBytes() + incoming > QuotaBytes)
                    return new UploadResult(UploadOutcome.QuotaExceeded, current);
            }

            File.WriteAllText(part + ".json", JsonSerializer.Serialize(new PartMeta(size, mtimeMs), Json));
            long length;
            await using (var fs = new FileStream(part, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int n;
                while ((n = await body.ReadAsync(buffer)) > 0)
                {
                    if (fs.Length + n > size) { fs.Close(); DeletePart(path); return new UploadResult(UploadOutcome.Corrupt, 0); }
                    await fs.WriteAsync(buffer.AsMemory(0, n));
                }
                length = fs.Length;
            }

            if (length < size) return new UploadResult(UploadOutcome.Partial, length);

            string actual;
            await using (var rs = File.OpenRead(part))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(rs)).ToLowerInvariant();
            if (sha256Hex is null || !string.Equals(actual, sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                DeletePart(path);
                return new UploadResult(UploadOutcome.Corrupt, 0);
            }

            var stored = _state.Index.TryGetValue(path, out var existing) ? existing.Stored : StoredPathFor(path);
            var target = Path.Combine(_deviceDir, Native(stored));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(part, target, overwrite: true);
            File.Delete(part + ".json");
            File.SetLastWriteTimeUtc(target, DateTimeOffset.FromUnixTimeMilliseconds(mtimeMs).UtcDateTime);
            _state.Index[path] = new IndexEntry(stored, size, mtimeMs);
            Save();

            var share = QuotaBytes > 0 ? (double)UsedBytes() / QuotaBytes : 0;
            if (share >= WarnShare) QuotaWarning?.Invoke(share);
            Changed?.Invoke();
            return new UploadResult(UploadOutcome.Complete, size);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Complete() => WithLock(() =>
    {
        _state.LastBackupUtc = _utcNow();
        PurgeTrash(all: false);
        Save();
        Changed?.Invoke();
        return 0;
    });

    public void ConfirmHeldBackDeletions() => WithLock(() =>
    {
        foreach (var k in _state.HeldBack) MoveToTrash(k);
        _state.HeldBack = new();
        Save();
        Changed?.Invoke();
        return 0;
    });

    public void DiscardHeldBackDeletions() => WithLock(() =>
    {
        _state.HeldBack = new();
        Save();
        Changed?.Invoke();
        return 0;
    });

    public StatusDto GetStatus() => WithLock(() =>
        new StatusDto(UsedBytes(), QuotaBytes, DirSize(_trashDir), _state.LastBackupUtc, _state.HeldBack.Count > 0));

    // ---- intern ----

    private T WithLock<T>(Func<T> f)
    {
        _lock.Wait();
        try { return f(); } finally { _lock.Release(); }
    }

    /// <summary>Index speichert Windows-Pfade ('\\'); für das laufende System umwandeln.</summary>
    private static string Native(string stored) => stored.Replace('\\', Path.DirectorySeparatorChar);

    private static bool UnderRoot(string path, string root) =>
        path.StartsWith(root + "/", StringComparison.Ordinal);

    private string PartPath(string path)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant();
        return Path.Combine(_partsDir, hash + ".part");
    }

    /// <summary>Vorhandene Bytes eines passenden Teils; ein Teil zu anderer Größe/Zeit wird verworfen.</summary>
    private long PartOffset(string path, long size, long mtimeMs)
    {
        var part = PartPath(path);
        if (!File.Exists(part)) return 0;
        var metaFile = part + ".json";
        var meta = File.Exists(metaFile) ? JsonSerializer.Deserialize<PartMeta>(File.ReadAllText(metaFile), Json) : null;
        if (meta is null || meta.Size != size || meta.MtimeMs != mtimeMs)
        {
            DeletePart(path);
            return 0;
        }
        return new FileInfo(part).Length;
    }

    private void DeletePart(string path)
    {
        var part = PartPath(path);
        File.Delete(part);
        File.Delete(part + ".json");
    }

    private string StoredPathFor(string phonePath)
    {
        var rel = PathMapper.ToWindowsRelative(phonePath);
        var taken = new HashSet<string>(_state.Index.Values.Select(v => v.Stored), StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(rel)) return rel;
        var slash = rel.LastIndexOf('\\');
        var dir = slash >= 0 ? rel[..(slash + 1)] : "";
        var file = rel[(slash + 1)..];
        var dot = file.LastIndexOf('.');
        var name = dot > 0 ? file[..dot] : file;
        var ext = dot > 0 ? file[dot..] : "";
        for (var i = 2; ; i++)
        {
            var candidate = $"{dir}{name}~{i}{ext}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private void MoveToTrash(string phonePath)
    {
        if (!_state.Index.Remove(phonePath, out var e)) return;
        var source = Path.Combine(_deviceDir, Native(e.Stored));
        if (!File.Exists(source)) return;
        var day = _utcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var target = Path.Combine(_trashDir, day, Native(e.Stored));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target, overwrite: true);
    }

    private void PurgeTrash(bool all)
    {
        if (!Directory.Exists(_trashDir)) return;
        var cutoff = _utcNow().Date.AddDays(-TrashDays);
        foreach (var dir in Directory.GetDirectories(_trashDir))
        {
            var ok = DateTime.TryParseExact(Path.GetFileName(dir), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date);
            if (all || (ok && date <= cutoff))
                Directory.Delete(dir, recursive: true);
        }
    }

    private long UsedBytes() => _state.Index.Values.Sum(v => v.Size) + DirSize(_trashDir) + DirSize(_partsDir, "*.part");

    private static long DirSize(string dir, string pattern = "*") =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : 0;

    private void Save()
    {
        var tmp = _statePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_state, Json));
        File.Move(tmp, _statePath, overwrite: true);
    }
}
