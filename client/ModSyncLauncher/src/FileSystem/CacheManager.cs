using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModSyncLauncher.Core;

namespace ModSyncLauncher.FileSystem;

public class CacheManager
{
    private readonly string _cacheDirectory;

    public CacheManager(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public string GetCacheFilePath(ModManifestItem mod, ModFileItem file)
    {
        var sanitizedFileName = Path.GetFileName(file.Path);
        var cacheFileName = $"{mod.Id}_{file.Sha256}_{sanitizedFileName}";
        return Path.Combine(_cacheDirectory, cacheFileName);
    }

    public async Task<bool> IsCachedAndValidAsync(ModManifestItem mod, ModFileItem file, CancellationToken cancellationToken = default)
    {
        var cachePath = GetCacheFilePath(mod, file);
        if (!File.Exists(cachePath))
            return false;

        return await HashValidator.ValidateFileAsync(cachePath, file.Sha256, cancellationToken);
    }

    public void CleanOldVersions(ModManifestItem currentMod, ModFileItem currentFile)
    {
        try
        {
            var fileName = Path.GetFileName(currentFile.Path);
            var searchPattern = $"{currentMod.Id}_*_{fileName}";
            var files = Directory.GetFiles(_cacheDirectory, searchPattern);
            var currentCacheFile = GetCacheFilePath(currentMod, currentFile);

            foreach (var file in files)
            {
                if (!string.Equals(file, currentCacheFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Failed to clean old cache for {currentMod.Name} ({currentFile.Path}): {ex.Message}");
            Console.ResetColor();
        }
    }
}
