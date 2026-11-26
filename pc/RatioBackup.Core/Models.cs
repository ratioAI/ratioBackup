namespace RatioBackup.Core;

public record FileEntry(string Path, long Size, long MtimeMs);
public record ManifestRequest(List<string> Roots, List<FileEntry> Files);
public record NeededFile(string Path, long Offset);
public record ManifestResult(List<NeededFile> Needed, int Trashed, int HeldBackDeletions);
public record StatusDto(long UsedBytes, long QuotaBytes, long TrashBytes, DateTime? LastBackupUtc, bool PendingDeletionReview);
public record PairRequest(string Code, string DeviceName);
public record PairResponse(string Token, string Proof);
public record OffsetDto(long Offset);

public enum UploadOutcome { Complete, Partial, WrongOffset, Corrupt, QuotaExceeded }
public record UploadResult(UploadOutcome Outcome, long Offset);
