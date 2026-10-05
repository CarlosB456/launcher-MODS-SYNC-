using System;
using System.Collections.Concurrent;
using ModSyncLauncher.Core;
using ModSyncLauncher.Sync;

namespace ModSyncLauncher.UI;

public class ConsoleRenderer
{
    private const string Version = "0.4.0 - R1";
    private const string Credits = "eLdarqO";

    private readonly ConcurrentDictionary<string, double> _downloadProgress = new();
    private readonly object _consoleLock = new();
    
    private static void SafeClear()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
        }
        catch { }
    }

    public void DisplayHeader(string serverName, string serverId, int modCount)
    {
        lock (_consoleLock)
        {
            SafeClear();
            try { Console.ForegroundColor = ConsoleColor.Cyan; } catch { }
            Console.WriteLine("==================================================");
            Console.WriteLine($"  open.mp Mod Sync v{Version}");
            Console.WriteLine($"  Credits: {Credits}");
            Console.WriteLine($"  Server: {serverName} [{serverId}]");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Total mods to sync: {modCount}");
            Console.WriteLine("==================================================\n");
            try { Console.ResetColor(); } catch { }
        }
    }

    public void UpdateStatus(string status)
    {
        lock (_consoleLock)
        {
            Console.WriteLine($"[INFO] {status}");
        }
    }

    public void UpdateProgress(ModManifestItem mod, double progressPercentage)
    {
        _downloadProgress[mod.Id] = progressPercentage;

        if (progressPercentage >= 100)
        {
            lock (_consoleLock)
            {
                Console.WriteLine($"[DOWNLOAD] {mod.Name} completed (100%).");
            }
        }
    }

    public void ModProcessed(ModManifestItem mod, bool success, string message)
    {
        lock (_consoleLock)
        {
            try { Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red; } catch { }
            var status = success ? "OK" : "ERROR";
            Console.WriteLine($"[{status}] {mod.Name} - {message}");
            try { Console.ResetColor(); } catch { }
        }
    }

    public void DisplaySummary(SyncResult result)
    {
        lock (_consoleLock)
        {
            Console.WriteLine("\n==================================================");
            try { Console.ForegroundColor = result.Status == SyncStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow; } catch { }
            Console.WriteLine($"Sync Result: {result.Status}");
            try { Console.ResetColor(); } catch { }
            Console.WriteLine($"Version: {Version} | Credits: {Credits}");
            Console.WriteLine($"Message: {result.Message}");
            Console.WriteLine($"Downloaded: {result.DownloadedModsCount}");
            Console.WriteLine($"Loaded from Cache: {result.CachedModsCount}");
            Console.WriteLine($"Successfully Installed: {result.InstalledModsCount}");
            Console.WriteLine($"Failed: {result.FailedModsCount}");
            Console.WriteLine("==================================================\n");
        }
    }
}
