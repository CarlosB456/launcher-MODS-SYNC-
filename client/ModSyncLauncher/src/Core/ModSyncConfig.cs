namespace ModSyncLauncher.Core;

public class ModSyncConfig
{
    public string GtaPath { get; set; } = string.Empty;
    public string ServerAddress { get; set; } = string.Empty;
    public string CdnUrl { get; set; } = string.Empty;
    public string CacheDirectory { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public int MaxConcurrentDownloads { get; set; } = 4;
    public bool AutoLaunchGame { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string PlayerName { get; set; } = "ModSync_Player";
    public string ServerHost { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 7777;
    public bool DeactivateSession { get; set; } = false;
    public bool CleanOnExit { get; set; } = false;
    public bool SyncOnly { get; set; } = false;
}
