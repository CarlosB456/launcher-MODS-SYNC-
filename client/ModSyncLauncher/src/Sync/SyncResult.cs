namespace ModSyncLauncher.Sync;

public enum SyncStatus
{
    Success,
    Failed,
    Cancelled
}

public class SyncResult
{
    public SyncStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DownloadedModsCount { get; set; }
    public int InstalledModsCount { get; set; }
    public int CachedModsCount { get; set; }
    public int FailedModsCount { get; set; }
}
