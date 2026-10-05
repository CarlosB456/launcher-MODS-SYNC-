using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModSyncLauncher.Core;
using ModSyncLauncher.FileSystem;
using ModSyncLauncher.Network;

namespace ModSyncLauncher.Sync;

public class SyncEngine
{
    private readonly ModSyncConfig _config;
    private readonly ManifestClient _manifestClient;
    private readonly ModDownloader _downloader;
    private readonly CacheManager _cacheManager;
    private readonly ModInstaller _installer;

    public event Action<string>? OnSyncStatusChanged;
    public event Action<ModManifestItem, double>? OnModDownloadProgress;
    public event Action<ModManifestItem, bool, string>? OnModProcessed;
    public event Action<ModManifest>? OnManifestLoaded;

    public SyncEngine(ModSyncConfig config, ManifestClient manifestClient, ModDownloader downloader, CacheManager cacheManager, ModInstaller installer)
    {
        _config = config;
        _manifestClient = manifestClient;
        _downloader = downloader;
        _cacheManager = cacheManager;
        _installer = installer;
    }

    public async Task<SyncResult> RunSyncAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { Status = SyncStatus.Success };

        try
        {
            OnSyncStatusChanged?.Invoke("Fetching manifest from server...");
            var manifest = await _manifestClient.GetManifestAsync(cancellationToken);

            if (manifest == null || manifest.Mods == null)
            {
                result.Status = SyncStatus.Failed;
                result.Message = "Failed to load mod manifest from server.";
                return result;
            }

            var serverId = !string.IsNullOrWhiteSpace(_config.ServerId) ? _config.ServerId : manifest.EffectiveServerId;
            _installer.ServerId = serverId;

            OnManifestLoaded?.Invoke(manifest);

            var semaphore = new SemaphoreSlim(_config.MaxConcurrentDownloads);
            var tasks = new List<Task>();

            foreach (var mod in manifest.Mods)
            {
                tasks.Add(ProcessModAsync(mod, semaphore, result, cancellationToken));
            }

            await Task.WhenAll(tasks);

            if (result.FailedModsCount > 0)
            {
                result.Status = SyncStatus.Failed;
                result.Message = $"Sync completed with {result.FailedModsCount} errors.";
            }
            else
            {
                // Activate server session (modloader server profile + CLEO staged session)
                _installer.ActivateServerSession(serverId);
                OnSyncStatusChanged?.Invoke($"Server session activated for [{serverId}].");
                result.Message = "Sync completed successfully.";
            }
        }
        catch (OperationCanceledException)
        {
            result.Status = SyncStatus.Cancelled;
            result.Message = "Sync was cancelled.";
        }
        catch (Exception ex)
        {
            result.Status = SyncStatus.Failed;
            result.Message = $"Unexpected error during sync: {ex.Message}";
        }

        return result;
    }

    private async Task ProcessModAsync(ModManifestItem mod, SemaphoreSlim semaphore, SyncResult result, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var files = mod.GetEffectiveFiles().ToList();
            if (files.Count == 0)
            {
                lock (result) { result.CachedModsCount++; }
                OnModProcessed?.Invoke(mod, true, "No files declared");
                return;
            }

            bool allFilesOk = true;
            bool wasDownloaded = false;
            string baseUrl = !string.IsNullOrEmpty(_config.CdnUrl) ? _config.CdnUrl : _config.ServerAddress;
            baseUrl = baseUrl.TrimEnd('/');

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedUri))
            {
                if (parsedUri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    parsedUri.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = $"{parsedUri.Scheme}://{parsedUri.Authority}";
                    var dir = Path.GetDirectoryName(parsedUri.AbsolutePath)?.Replace('\\', '/').Trim('/');
                    if (!string.IsNullOrEmpty(dir))
                    {
                        baseUrl += $"/{dir}";
                    }
                }
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isCached = await _cacheManager.IsCachedAndValidAsync(mod, file, cancellationToken);
                string cachePath = _cacheManager.GetCacheFilePath(mod, file);

                if (!isCached)
                {
                    OnSyncStatusChanged?.Invoke($"Downloading {mod.Name} -> {Path.GetFileName(file.Path)}...");
                    
                    // Format: /api/download/{mod.Id}/{file.Path}
                    var downloadUrl = $"{baseUrl}/api/download/{mod.Id}/{file.Path}";

                    var downloadOk = await _downloader.DownloadFileAsync(downloadUrl, cachePath, (progress) =>
                    {
                        OnModDownloadProgress?.Invoke(mod, progress);
                    }, cancellationToken);

                    if (!downloadOk)
                    {
                        allFilesOk = false;
                        OnModProcessed?.Invoke(mod, false, $"Download failed for {file.Path}");
                        break;
                    }

                    if (!string.IsNullOrEmpty(file.Sha256))
                    {
                        bool isValidHash = await HashValidator.ValidateFileAsync(cachePath, file.Sha256, cancellationToken);
                        if (!isValidHash)
                        {
                            try { if (File.Exists(cachePath)) File.Delete(cachePath); } catch { }
                            allFilesOk = false;
                            OnModProcessed?.Invoke(mod, false, $"Hash mismatch for {file.Path}");
                            break;
                        }
                    }

                    wasDownloaded = true;
                }

                bool installed = _installer.InstallFile(mod, file, cachePath);
                if (!installed)
                {
                    allFilesOk = false;
                    OnModProcessed?.Invoke(mod, false, $"Failed to install {file.Path}");
                    break;
                }

                _cacheManager.CleanOldVersions(mod, file);
            }

            lock (result)
            {
                if (allFilesOk)
                {
                    result.InstalledModsCount++;
                    if (wasDownloaded)
                        result.DownloadedModsCount++;
                    else
                        result.CachedModsCount++;

                    OnModProcessed?.Invoke(mod, true, wasDownloaded ? "Downloaded and installed" : "Synchronized from cache");
                }
                else
                {
                    result.FailedModsCount++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (result) { result.FailedModsCount++; }
            OnModProcessed?.Invoke(mod, false, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
