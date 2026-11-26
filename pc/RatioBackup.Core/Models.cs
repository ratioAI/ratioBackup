namespace RatioBackup.Core;

public record FileEntry(string Path, long Size, long MtimeMs);
public record ManifestRequest(List<string> Roots, List<FileEntry> Files);
public record NeededFile(string Path, long Offset);
public record ManifestResult(List<NeededFile> Needed, int Trashed, int HeldBackDeletions);
public record StatusDto(long UsedBytes, long QuotaBytes, long TrashBytes, DateTime? LastBackupUtc, bool PendingDeletionReview, RunInfo? Run = null);
public record RunInfo(bool Active, int TotalFiles, int DoneFiles, long TotalBytes, long DoneBytes, DateTime StartedUtc, DateTime LastActivityUtc);
public record RunRecord(DateTime StartUtc, DateTime EndUtc, int Files, long Bytes, string Result);
public record TrashEntry(string Id, string PhonePath, string Stored, string Day, long Size, DateTime DeletedUtc);
public record PairRequest(string Code, string DeviceName);
public record OffsetDto(long Offset);

public enum UploadOutcome { Complete, Partial, WrongOffset, Corrupt, QuotaExceeded }
public record UploadResult(UploadOutcome Outcome, long Offset);
