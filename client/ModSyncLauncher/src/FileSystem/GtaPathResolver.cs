using System;
using System.IO;
using Microsoft.Win32;

namespace ModSyncLauncher.FileSystem;

public static class GtaPathResolver
{
    public static string? FindGtaPath()
    {
        string?[] pathsToCheck = 
        {
            GetRegistryPath(),
            @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto San Andreas",
            @"C:\Program Files\Steam\steamapps\common\Grand Theft Auto San Andreas",
            @"C:\Program Files\Rockstar Games\GTA San Andreas",
            @"C:\Program Files (x86)\Rockstar Games\GTA San Andreas",
            @"D:\SteamLibrary\steamapps\common\Grand Theft Auto San Andreas"
        };

        foreach (var path in pathsToCheck)
        {
            if (IsValidGtaPath(path))
            {
                return path;
            }
        }

        return null;
    }

    public static bool IsValidGtaPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;

        return File.Exists(Path.Combine(path, "gta_sa.exe"));
    }

    private static string? GetRegistryPath()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\SAMP");
                if (key != null)
                {
                    var path = key.GetValue("gta_sa_exe") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        return Path.GetDirectoryName(path);
                    }
                }
                
                using var key2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\GTA San Andreas\Installation");
                if (key2 != null)
                {
                    return key2.GetValue("ExePath") as string;
                }
            }
        }
        catch
        {
            // Ignore registry errors
        }
        
        return null;
    }
}
