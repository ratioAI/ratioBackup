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
        public List<RunRecord> History { get; set; } = new();
        public List<TrashEntry> Trash { get; set; } = new();
        public List<KeptEntry> Kept { get; set; } = new();
    }

    /// <summary>Aus dem Papierkorb wiederhergestellt: bleibt dauerhaft am PC, wird nie automatisch gelöscht.</summary>
    public sealed record KeptEntry(string Stored, long Size);

    private sealed class RunState
    {
        public bool Active;
        public int TotalFiles, DoneFiles;
        public long TotalBytes, DoneBytes;
        public DateTime Started, LastActivity;
    }

    public const int HistoryMax = 100;
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(10);
    private readonly RunState _run = new();
    private volatile IReadOnlyList<RunRecord> _historySnapshot = Array.Empty<RunRecord>();
    private volatile IReadOnlyList<TrashEntry> _trashSnapshot = Array.Empty<TrashEntry>();

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
    private volatile StatusDto _snapshot = new(0, 0, 0, null, false);
    private volatile IReadOnlyList<string> _heldSnapshot = Array.Empty<string>();
    private int _unsaved;
    private DateTime _lastSave = DateTime.MinValue;
    public const int SaveEveryFiles = 200;
    public static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(5);

    /// <summary>Anzahl Index-Schreibvorgänge (für Tests).</summary>
    internal int SaveCount { get; private set; }

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
        Refresh();
    }

    /// <summary>Stand der zurückgehaltenen Löschungen; blockiert nie (auch nicht während eines Uploads).</summary>
    public IReadOnlyList<string> HeldBackDeletions => _heldSnapshot;

    /// <summary>Letzte Läufe, neueste zuerst; blockiert nie.</summary>
    public IReadOnlyList<RunRecord> History { get { CheckTimeout(); return _historySnapshot; } }

    /// <summary>Papierkorb, zuletzt gelöschte zuerst; blockiert nie.</summary>
    public IReadOnlyList<TrashEntry> ListTrash() => _trashSnapshot;

    public bool Restore(string id) => WithLock(() =>
    {
        var e = _state.Trash.FirstOrDefault(t => t.Id == id);
        if (e is null) return false;
        var source = Path.Combine(_trashDir, e.Day, Native(e.Stored));
        if (!File.Exists(source)) { _state.Trash.Remove(e); Save(); Refresh(); return false; }
        var stored = UniqueStored(e.Stored, avoidDiskFiles: true);
        var target = Path.Combine(_deviceDir, Native(stored));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // z. B. Datei gerade geöffnet – Eintrag bleibt im Papierkorb
        }
        _state.Trash.Remove(e);
        _state.Kept.Add(new KeptEntry(stored, e.Size));
        Save();
        Refresh();
        Changed?.Invoke();
        return true;
    });

    public void DeletePermanently(IEnumerable<string> ids) => WithLock(() =>
    {
        var set = ids.ToHashSet();
        foreach (var e in _state.Trash.Where(t => set.Contains(t.Id)).ToList())
        {
            try { File.Delete(Path.Combine(_trashDir, e.Day, Native(e.Stored))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            _state.Trash.Remove(e);
        }
        Save();
        Refresh();
        Changed?.Invoke();
        return 0;
    });

    public ManifestResult ApplyManifest(ManifestRequest m) => WithLock(() =>
    {
        if (_run.Active) FinishRun("interrupted");
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
        var now = _utcNow();
        _run.Active = true;
        _run.TotalFiles = needed.Count;
        _run.DoneFiles = 0;
        _run.TotalBytes = needed.Sum(n => Math.Max(0, onPhone[n.Path].Size - n.Offset));
        _run.DoneBytes = 0;
        _run.Started = now;
        _run.LastActivity = now;
        Save();
        Refresh();
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
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length;
            await using (var fs = new FileStream(part, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var buffer = new byte[81920];
                int n;
                // Vorhandenen Teil einmal in die Prüfsumme aufnehmen, danach nur noch neue Bytes.
                while ((n = await fs.ReadAsync(buffer)) > 0) hash.AppendData(buffer, 0, n);
                while ((n = await body.ReadAsync(buffer)) > 0)
                {
                    if (fs.Length + n > size) { fs.Close(); DeletePart(path); return new UploadResult(UploadOutcome.Corrupt, 0); }
                    await fs.WriteAsync(buffer.AsMemory(0, n));
                    hash.AppendData(buffer, 0, n);
                    if (_run.Active) { _run.DoneBytes += n; _run.LastActivity = _utcNow(); }
                }
                length = fs.Length;
            }

            if (length < size) return new UploadResult(UploadOutcome.Partial, length);

            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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
            if (_run.Active) _run.DoneFiles++;
            MaybeSave();

            var share = QuotaBytes > 0 ? (double)UsedBytes() / QuotaBytes : 0;
            if (share >= WarnShare) QuotaWarning?.Invoke(share);
            Changed?.Invoke();
            return new UploadResult(UploadOutcome.Complete, size);
        }
        finally
        {
            Refresh();
            _lock.Release();
        }
    }

    /// <summary>Index sofort speichern (z. B. beim Beenden des Programms).</summary>
    public void Flush() => WithLock(() => { if (_unsaved > 0) Save(); return 0; });

    public void Complete() => WithLock(() =>
    {
        _state.LastBackupUtc = _utcNow();
        if (_run.Active) FinishRun("ok");
        PurgeTrash(all: false);
        Save();
        Refresh();
        Changed?.Invoke();
        return 0;
    });

    public void ConfirmHeldBackDeletions() => WithLock(() =>
    {
        foreach (var k in _state.HeldBack) MoveToTrash(k);
        _state.HeldBack = new();
        Save();
        Refresh();
        Changed?.Invoke();
        return 0;
    });

    public void DiscardHeldBackDeletions() => WithLock(() =>
    {
        _state.HeldBack = new();
        Save();
        Refresh();
        Changed?.Invoke();
        return 0;
    });

    /// <summary>Letzter bekannter Stand; blockiert nie (auch nicht während eines Uploads).</summary>
    public StatusDto GetStatus()
    {
        CheckTimeout();
        return _snapshot with { QuotaBytes = QuotaBytes };
    }

    /// <summary>Ein Lauf ohne Aktivität seit 2 Minuten gilt als unterbrochen (Handy weg, WLAN aus).</summary>
    private void CheckTimeout()
    {
        var snap = _snapshot.Run;
        if (snap is not { Active: true } || _utcNow() - snap.LastActivityUtc <= RunTimeout) return;
        if (!_lock.Wait(0)) return; // gerade aktiv – dann ist der Lauf ohnehin nicht abgelaufen
        try
        {
            if (_run.Active && _utcNow() - _run.LastActivity > RunTimeout)
            {
                FinishRun("interrupted");
                Save();
                Refresh();
            }
        }
        finally { _lock.Release(); }
    }

    private void FinishRun(string result)
    {
        _run.Active = false;
        _state.History.Insert(0, new RunRecord(_run.Started, result == "ok" ? _utcNow() : _run.LastActivity, _run.DoneFiles, _run.DoneBytes, result));
        if (_state.History.Count > HistoryMax) _state.History.RemoveRange(HistoryMax, _state.History.Count - HistoryMax);
    }

    /// <summary>Nur unter der Sperre aufrufen.</summary>
    private void Refresh()
    {
        var run = new RunInfo(_run.Active, _run.TotalFiles, _run.DoneFiles, _run.TotalBytes, _run.DoneBytes, _run.Started, _run.LastActivity);
        _snapshot = new StatusDto(UsedBytes(), QuotaBytes, DirSize(_trashDir), _state.LastBackupUtc, _state.HeldBack.Count > 0, run);
        _heldSnapshot = _state.HeldBack.ToList();
        _historySnapshot = _state.History.ToList();
        _trashSnapshot = _state.Trash.OrderByDescending(t => t.DeletedUtc).ToList();
    }

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

    private string StoredPathFor(string phonePath) => UniqueStored(PathMapper.ToWindowsRelative(phonePath), avoidDiskFiles: false);

    /// <summary>Freier Ablagepfad: belegt durch Index, wiederhergestellte Dateien oder eine Datei auf der Platte → "~2", "~3" …</summary>
    /// <remarks>
    /// Beim Hochladen zählen nur Index und behaltene Dateien als belegt: Eine nicht erfasste Datei auf der Platte ist
    /// meist ein Upload, dessen Index-Eintrag vor einem Absturz nicht mehr gespeichert wurde – sie wird überschrieben.
    /// Beim Wiederherstellen wird nichts überschrieben.
    /// </remarks>
    private string UniqueStored(string rel, bool avoidDiskFiles)
    {
        var taken = new HashSet<string>(_state.Index.Values.Select(v => v.Stored).Concat(_state.Kept.Select(k => k.Stored)), StringComparer.OrdinalIgnoreCase);
        bool Free(string c) => !taken.Contains(c) && (!avoidDiskFiles || !File.Exists(Path.Combine(_deviceDir, Native(c))));
        if (Free(rel)) return rel;
        var slash = rel.LastIndexOf('\\');
        var dir = slash >= 0 ? rel[..(slash + 1)] : "";
        var file = rel[(slash + 1)..];
        var dot = file.LastIndexOf('.');
        var name = dot > 0 ? file[..dot] : file;
        var ext = dot > 0 ? file[dot..] : "";
        for (var i = 2; ; i++)
        {
            var candidate = $"{dir}{name}~{i}{ext}";
            if (Free(candidate)) return candidate;
        }
    }

    private void MoveToTrash(string phonePath)
    {
        if (!_state.Index.Remove(phonePath, out var e)) return;
        var source = Path.Combine(_deviceDir, Native(e.Stored));
        if (!File.Exists(source)) return;
        var day = _utcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var target = Path.Combine(_trashDir, day, Native(e.Stored));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _state.Index[phonePath] = e; // gesperrt: bleibt im Spiegel, nächster Lauf versucht es erneut
            return;
        }
        _state.Trash.RemoveAll(t => t.Day == day && string.Equals(t.Stored, e.Stored, StringComparison.OrdinalIgnoreCase));
        _state.Trash.Add(new TrashEntry(Guid.NewGuid().ToString("N"), phonePath, e.Stored, day, e.Size, _utcNow()));
    }

    private void PurgeTrash(bool all)
    {
        if (!Directory.Exists(_trashDir)) return;
        var cutoff = _utcNow().Date.AddDays(-TrashDays);
        foreach (var dir in Directory.GetDirectories(_trashDir))
        {
            var ok = DateTime.TryParseExact(Path.GetFileName(dir), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date);
            if (all || (ok && date < cutoff))
            {
                Directory.Delete(dir, recursive: true);
                var day = Path.GetFileName(dir);
                _state.Trash.RemoveAll(t => t.Day == day);
            }
        }
    }

    private long UsedBytes() => _state.Index.Values.Sum(v => v.Size) + _state.Kept.Sum(k => k.Size) + DirSize(_trashDir) + DirSize(_partsDir, "*.part");

    private static long DirSize(string dir, string pattern = "*") =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : 0;

    private void MaybeSave()
    {
        _unsaved++;
        if (_unsaved >= SaveEveryFiles || _utcNow() - _lastSave >= SaveEvery) Save();
    }

    private void Save()
    {
        _unsaved = 0;
        _lastSave = _utcNow();
        SaveCount++;
        var tmp = _statePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_state, Json));
        File.Move(tmp, _statePath, overwrite: true);
    }
}
