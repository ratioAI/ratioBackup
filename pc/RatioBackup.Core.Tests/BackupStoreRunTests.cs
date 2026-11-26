using System.Security.Cryptography;

namespace RatioBackup.Core.Tests;

public sealed class BackupStoreRunTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rbr-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 11, 26, 19, 0, 0, DateTimeKind.Utc);
    private const long Mtime = 1764180000000;
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private BackupStore NewStore() => new(_root, "Xiaomi 18 Pro", () => _now);
    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    private static byte[] Bytes(int n, byte f = 7) => Enumerable.Repeat(f, n).ToArray();
    private static Task<UploadResult> Put(BackupStore s, string p, byte[] d) => s.WriteAsync(p, d.Length, Mtime, 0, new MemoryStream(d), Sha(d));
    private static ManifestRequest M(params (string P, long S)[] f) =>
        new(new() { "DCIM" }, f.Select(x => new FileEntry(x.P, x.S, Mtime)).ToList());
    private string Dev(string rel) => Path.Combine(_root, "Xiaomi 18 Pro", rel);

    [Fact]
    public async Task Run_StartsOnManifest_CountsBytes()
    {
        var s = NewStore();
        s.ApplyManifest(M(("DCIM/a.jpg", 10), ("DCIM/b.jpg", 30)));
        var run = s.GetStatus().Run!;
        Assert.True(run.Active);
        Assert.Equal(2, run.TotalFiles);
        Assert.Equal(40, run.TotalBytes);
        await Put(s, "DCIM/a.jpg", Bytes(10));
        run = s.GetStatus().Run!;
        Assert.Equal(1, run.DoneFiles);
        Assert.Equal(10, run.DoneBytes);
    }

    [Fact]
    public async Task Run_CompleteRecordsHistory()
    {
        var s = NewStore();
        s.ApplyManifest(M(("DCIM/a.jpg", 10)));
        await Put(s, "DCIM/a.jpg", Bytes(10));
        _now = _now.AddMinutes(3);
        s.Complete();
        Assert.False(s.GetStatus().Run?.Active ?? false);
        var h = s.History.Single();
        Assert.Equal("ok", h.Result);
        Assert.Equal(1, h.Files);
        Assert.Equal(10, h.Bytes);
        Assert.Equal(TimeSpan.FromMinutes(3), h.EndUtc - h.StartUtc);
    }

    [Fact]
    public void Run_TimesOutAfterInactivity()
    {
        var s = NewStore();
        s.ApplyManifest(M(("DCIM/a.jpg", 10)));
        _now = _now.AddMinutes(2).AddSeconds(1);
        Assert.False(s.GetStatus().Run?.Active ?? false);
        Assert.Equal("interrupted", s.History.Single().Result);
    }

    [Fact]
    public void History_CappedAt100()
    {
        var s = NewStore();
        for (var i = 0; i < 105; i++) { s.ApplyManifest(M()); s.Complete(); _now = _now.AddMinutes(1); }
        Assert.Equal(100, s.History.Count);
        Assert.True(s.History[0].StartUtc > s.History[^1].StartUtc);
    }

    [Fact]
    public async Task Trash_ListsEntriesWithPhonePath()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        s.ApplyManifest(M());
        var e = s.ListTrash().Single();
        Assert.Equal("DCIM/a.jpg", e.PhonePath);
        Assert.Equal(5, e.Size);
        Assert.Equal("2026-11-26", e.Day);
    }

    [Fact]
    public async Task Restore_MovesBackAndKeepsForever()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        s.ApplyManifest(M());
        Assert.True(s.Restore(s.ListTrash().Single().Id));
        Assert.True(File.Exists(Dev(Path.Combine("DCIM", "a.jpg"))));
        Assert.Empty(s.ListTrash());
        Assert.Equal(0, s.ApplyManifest(M()).Trashed);
        Assert.True(File.Exists(Dev(Path.Combine("DCIM", "a.jpg"))));
    }

    [Fact]
    public async Task Restore_TargetExists_KeepsBoth()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5, 1));
        s.ApplyManifest(M());
        await Put(s, "DCIM/a.jpg", Bytes(6, 2));
        Assert.True(s.Restore(s.ListTrash().Single().Id));
        Assert.Equal(Bytes(6, 2), File.ReadAllBytes(Dev(Path.Combine("DCIM", "a.jpg"))));
        Assert.Contains(Directory.GetFiles(Dev("DCIM")), f => File.ReadAllBytes(f).SequenceEqual(Bytes(5, 1)));
    }

    [Fact]
    public async Task DeletePermanently_RemovesFileAndEntry()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        s.ApplyManifest(M());
        s.DeletePermanently(new[] { s.ListTrash().Single().Id });
        Assert.Empty(s.ListTrash());
        Assert.Equal(0, s.GetStatus().TrashBytes);
    }

    [Fact]
    public async Task Purge_RemovesEntries()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        s.ApplyManifest(M());
        _now = _now.AddDays(31);
        s.Complete();
        Assert.Empty(s.ListTrash());
    }
}
