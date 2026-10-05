using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ModSyncLauncher.Core;

public static class HashValidator
{
    public static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var sha256 = SHA256.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        
        var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static async Task<bool> ValidateFileAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default)
    {
        var actualHash = await ComputeFileHashAsync(filePath, cancellationToken);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
