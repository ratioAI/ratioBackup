using System.Security.Cryptography;
using System.Text;

namespace RatioBackup.Core.Tests;

public sealed class BackupStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rb-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 11, 26, 19, 0, 0, DateTimeKind.Utc);
    private const long Mtime = 1764180000000;

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private BackupStore NewStore() => new(_root, "Xiaomi 18 Pro", () => _now);
    private string Dev(string rel) => Path.Combine(_root, "Xiaomi 18 Pro", rel);

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private static async Task<UploadResult> Put(BackupStore s, string path, byte[] data, long mtime = Mtime)
        => await s.WriteAsync(path, data.Length, mtime, 0, new MemoryStream(data), Sha(data));

    private static ManifestRequest Manifest(params (string Path, long Size)[] files)
        => new(new List<string> { "DCIM", "Download" }, files.Select(f => new FileEntry(f.Path, f.Size, Mtime)).ToList());

    private static byte[] Bytes(int n, byte fill = 7) => Enumerable.Repeat(fill, n).ToArray();

    [Fact]
    public void NewFile_IsNeededWithOffsetZero()
    {
        var r = NewStore().ApplyManifest(Manifest(("DCIM/a.jpg", 5)));
        Assert.Equal(new[] { new NeededFile("DCIM/a.jpg", 0) }, r.Needed);
    }

    [Fact]
    public async Task UnchangedFile_NotNeeded()
    {
        var s = NewStore();
        Assert.Equal(UploadOutcome.Complete, (await Put(s, "DCIM/a.jpg", Bytes(5))).Outcome);
        Assert.Empty(s.ApplyManifest(Manifest(("DCIM/a.jpg", 5))).Needed);
    }

    [Fact]
    public async Task ChangedMtime_IsNeeded()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        var m = new ManifestRequest(new() { "DCIM" }, new() { new FileEntry("DCIM/a.jpg", 5, Mtime + 1000) });
        Assert.Single(s.ApplyManifest(m).Needed);
    }

    [Fact]
    public async Task Write_CompletesAndSetsMtime()
    {
        var s = NewStore();
        await Put(s, "DCIM/Camera/a.jpg", Bytes(5));
        var f = Dev(Path.Combine("DCIM", "Camera", "a.jpg"));
        Assert.True(File.Exists(f));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(Mtime).UtcDateTime, File.GetLastWriteTimeUtc(f));
    }

    [Fact]
    public async Task Write_InTwoChunks_ResumesAtOffset()
    {
        var s = NewStore();
        var data = Bytes(10);
        var first = await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 0, new MemoryStream(data[..4]), null);
        Assert.Equal(new UploadResult(UploadOutcome.Partial, 4), first);

        var m = s.ApplyManifest(Manifest(("DCIM/v.mp4", 10)));
        Assert.Equal(4, m.Needed.Single().Offset);

        var second = await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 4, new MemoryStream(data[4..]), Sha(data));
        Assert.Equal(UploadOutcome.Complete, second.Outcome);
        Assert.Equal(data, File.ReadAllBytes(Dev(Path.Combine("DCIM", "v.mp4"))));
    }

    [Fact]
    public async Task Write_WrongOffset_ReturnsActualOffset()
    {
        var s = NewStore();
        await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 0, new MemoryStream(Bytes(4)), null);
        var r = await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 7, new MemoryStream(Bytes(3)), null);
        Assert.Equal(new UploadResult(UploadOutcome.WrongOffset, 4), r);
    }

    [Fact]
    public async Task Write_ShaMismatch_DiscardsPart()
    {
        var s = NewStore();
        var r = await s.WriteAsync("DCIM/a.jpg", 5, Mtime, 0, new MemoryStream(Bytes(5)), Sha(Bytes(5, 9)));
        Assert.Equal(UploadOutcome.Corrupt, r.Outcome);
        Assert.Equal(0, s.ApplyManifest(Manifest(("DCIM/a.jpg", 5))).Needed.Single().Offset);
        Assert.False(File.Exists(Dev(Path.Combine("DCIM", "a.jpg"))));
    }

    [Fact]
    public async Task Write_SizeMismatch_DiscardsPart()
    {
        var s = NewStore();
        var r = await s.WriteAsync("DCIM/a.jpg", 5, Mtime, 0, new MemoryStream(Bytes(8)), Sha(Bytes(8)));
        Assert.Equal(UploadOutcome.Corrupt, r.Outcome);
        Assert.Equal(0, s.ApplyManifest(Manifest(("DCIM/a.jpg", 5))).Needed.Single().Offset);
    }

    [Fact]
    public async Task Write_ChangedFileMetadata_RestartsPart()
    {
        var s = NewStore();
        await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 0, new MemoryStream(Bytes(4)), null);
        var m = new ManifestRequest(new() { "DCIM" }, new() { new FileEntry("DCIM/v.mp4", 12, Mtime + 5) });
        Assert.Equal(0, s.ApplyManifest(m).Needed.Single().Offset);
    }

    [Fact]
    public async Task PartialFile_NeverVisibleInDeviceFolder()
    {
        var s = NewStore();
        await s.WriteAsync("DCIM/v.mp4", 10, Mtime, 0, new MemoryStream(Bytes(4)), null);
        Assert.False(File.Exists(Dev(Path.Combine("DCIM", "v.mp4"))));
    }

    [Fact]
    public async Task MissingFile_MovedToTrashWithDate()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        var r = s.ApplyManifest(Manifest());
        Assert.Equal(1, r.Trashed);
        Assert.False(File.Exists(Dev(Path.Combine("DCIM", "a.jpg"))));
        Assert.True(File.Exists(Path.Combine(_root, "_Papierkorb", "2026-11-26", "DCIM", "a.jpg")));
    }

    [Fact]
    public async Task Deletion_OnlyWithinListedRoots()
    {
        var s = NewStore();
        await Put(s, "Download/x.pdf", Bytes(5));
        var r = s.ApplyManifest(new ManifestRequest(new() { "DCIM" }, new()));
        Assert.Equal(0, r.Trashed);
        Assert.True(File.Exists(Dev(Path.Combine("Download", "x.pdf"))));
    }

    [Fact]
    public async Task Deletion_RootPrefixDoesNotMatchSiblingFolder()
    {
        var s = NewStore();
        await Put(s, "DCIMX/x.jpg", Bytes(5));
        var r = s.ApplyManifest(new ManifestRequest(new() { "DCIM" }, new()));
        Assert.Equal(0, r.Trashed);
    }

    [Fact]
    public async Task MassDeletion_HeldBack()
    {
        var s = NewStore();
        for (var i = 0; i < 200; i++) await Put(s, $"DCIM/{i}.jpg", Bytes(1));
        var keep = Enumerable.Range(0, 140).Select(i => ($"DCIM/{i}.jpg", 1L)).ToArray();
        var r = s.ApplyManifest(Manifest(keep));
        Assert.Equal(0, r.Trashed);
        Assert.Equal(60, r.HeldBackDeletions);
        Assert.True(s.GetStatus().PendingDeletionReview);
        Assert.True(File.Exists(Dev(Path.Combine("DCIM", "199.jpg"))));
    }

    [Fact]
    public async Task MassDeletion_BelowFloor_Applied()
    {
        var s = NewStore();
        for (var i = 0; i < 10; i++) await Put(s, $"DCIM/{i}.jpg", Bytes(1));
        Assert.Equal(10, s.ApplyManifest(Manifest()).Trashed);
    }

    [Fact]
    public async Task ConfirmHeldBack_MovesToTrash()
    {
        var s = NewStore();
        for (var i = 0; i < 60; i++) await Put(s, $"DCIM/{i}.jpg", Bytes(1));
        s.ApplyManifest(Manifest());
        Assert.Equal(60, s.HeldBackDeletions.Count);
        s.ConfirmHeldBackDeletions();
        Assert.Empty(s.HeldBackDeletions);
        Assert.False(s.GetStatus().PendingDeletionReview);
        Assert.False(File.Exists(Dev(Path.Combine("DCIM", "0.jpg"))));
        Assert.True(File.Exists(Path.Combine(_root, "_Papierkorb", "2026-11-26", "DCIM", "0.jpg")));
    }

    [Fact]
    public async Task DiscardHeldBack_KeepsFiles()
    {
        var s = NewStore();
        for (var i = 0; i < 60; i++) await Put(s, $"DCIM/{i}.jpg", Bytes(1));
        s.ApplyManifest(Manifest());
        s.DiscardHeldBackDeletions();
        Assert.False(s.GetStatus().PendingDeletionReview);
        Assert.True(File.Exists(Dev(Path.Combine("DCIM", "0.jpg"))));
    }

    [Fact]
    public void TrashOlderThan30Days_PurgedOnComplete()
    {
        var s = NewStore();
        var old = Path.Combine(_root, "_Papierkorb", "2026-10-26");
        var young = Path.Combine(_root, "_Papierkorb", "2026-10-27");
        Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "a"), "x");
        Directory.CreateDirectory(young); File.WriteAllText(Path.Combine(young, "b"), "x");
        s.Complete();
        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(young));
        Assert.Equal(_now, s.GetStatus().LastBackupUtc);
    }

    [Fact]
    public async Task CaseCollision_BothKept()
    {
        var s = NewStore();
        await Put(s, "DCIM/IMG.jpg", Bytes(3, 1));
        await Put(s, "DCIM/img.JPG", Bytes(4, 2));
        Assert.Equal(Bytes(3, 1), File.ReadAllBytes(Dev(Path.Combine("DCIM", "IMG.jpg"))));
        Assert.Equal(Bytes(4, 2), File.ReadAllBytes(Dev(Path.Combine("DCIM", "img~2.JPG"))));
        Assert.Empty(s.ApplyManifest(Manifest(("DCIM/IMG.jpg", 3), ("DCIM/img.JPG", 4))).Needed);
    }

    [Fact]
    public async Task IndexSurvivesRestart()
    {
        await Put(NewStore(), "DCIM/a.jpg", Bytes(5));
        Assert.Empty(NewStore().ApplyManifest(Manifest(("DCIM/a.jpg", 5))).Needed);
    }

    [Fact]
    public async Task Quota_PurgesTrashFirst()
    {
        var s = NewStore();
        s.QuotaBytes = 100;
        await Put(s, "DCIM/old.jpg", Bytes(60));
        s.ApplyManifest(Manifest());                    // old.jpg → Papierkorb (60 Byte)
        var r = await Put(s, "DCIM/new.jpg", Bytes(60));
        Assert.Equal(UploadOutcome.Complete, r.Outcome);
        Assert.Equal(0, s.GetStatus().TrashBytes);
    }

    [Fact]
    public async Task Quota_ExceededAfterPurge_ReturnsQuotaExceeded()
    {
        var s = NewStore();
        s.QuotaBytes = 100;
        await Put(s, "DCIM/a.jpg", Bytes(60));
        var r = await Put(s, "DCIM/b.jpg", Bytes(60));
        Assert.Equal(UploadOutcome.QuotaExceeded, r.Outcome);
        Assert.True(File.Exists(Dev(Path.Combine("DCIM", "a.jpg"))));
        Assert.False(File.Exists(Dev(Path.Combine("DCIM", "b.jpg"))));
    }

    [Fact]
    public async Task QuotaWarning_FiresAt90Percent()
    {
        var s = NewStore();
        s.QuotaBytes = 100;
        double? warned = null;
        s.QuotaWarning += r => warned = r;
        await Put(s, "DCIM/a.jpg", Bytes(80));
        Assert.Null(warned);
        await Put(s, "DCIM/b.jpg", Bytes(15));
        Assert.NotNull(warned);
        Assert.True(warned >= 0.9);
    }

    [Fact]
    public async Task Status_ReportsUsedAndQuota()
    {
        var s = NewStore();
        await Put(s, "DCIM/a.jpg", Bytes(5));
        var st = s.GetStatus();
        Assert.Equal(5, st.UsedBytes);
        Assert.Equal(100L * 1024 * 1024 * 1024, st.QuotaBytes);
        Assert.Null(st.LastBackupUtc);
    }

    [Fact]
    public async Task UnsafePath_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() => NewStore().WriteAsync("../x", 1, Mtime, 0, new MemoryStream(new byte[1]), null));
}
