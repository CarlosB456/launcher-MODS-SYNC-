using System;
using System.IO;

namespace ModSyncLauncher.FileSystem;

/// <summary>
/// Utility for inspecting and modifying Portable Executable (PE) headers.
/// Ensures 32-bit game executables (such as gta_sa.exe) are marked with
/// IMAGE_FILE_LARGE_ADDRESS_AWARE (0x0020) to enable access to up to 4GB of virtual memory.
/// </summary>
public static class PeHeaderModifier
{
    public const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;

    /// <summary>
    /// Checks and sets the IMAGE_FILE_LARGE_ADDRESS_AWARE characteristic flag on the target PE executable.
    /// </summary>
    /// <param name="exePath">Absolute path to executable.</param>
    /// <param name="statusMessage">Detailed outcome message.</param>
    /// <returns>True if the executable is or was successfully made Large Address Aware.</returns>
    public static bool EnsureLargeAddressAware(string exePath, out string statusMessage)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            statusMessage = $"Executable file not found: {exePath}";
            return false;
        }

        try
        {
            using var stream = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            if (stream.Length < 0x40)
            {
                statusMessage = "Executable file size is below minimum DOS header length.";
                return false;
            }

            // Verify DOS Header ("MZ")
            ushort dosMagic = reader.ReadUInt16();
            if (dosMagic != 0x5A4D)
            {
                statusMessage = "Invalid DOS header signature (not a valid PE image).";
                return false;
            }

            // Read offset to PE header (e_lfanew at 0x3C)
            stream.Seek(0x3C, SeekOrigin.Begin);
            uint peOffset = reader.ReadUInt32();

            if (peOffset + 24 > stream.Length)
            {
                statusMessage = "PE header offset points beyond file boundaries.";
                return false;
            }

            // Verify PE Signature ("PE\0\0")
            stream.Seek(peOffset, SeekOrigin.Begin);
            uint peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550)
            {
                statusMessage = "Invalid PE signature (expected PE\\0\\0).";
                return false;
            }

            // Characteristics offset in COFF File Header is at peOffset + 4 (signature) + 18
            long characteristicsOffset = peOffset + 22;
            stream.Seek(characteristicsOffset, SeekOrigin.Begin);
            ushort characteristics = reader.ReadUInt16();

            if ((characteristics & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0)
            {
                statusMessage = "IMAGE_FILE_LARGE_ADDRESS_AWARE (0x0020) is already active (4GB address space enabled).";
                return true;
            }

            // Create safety backup if not already present
            var backupPath = exePath + ".bak";
            if (!File.Exists(backupPath))
            {
                try
                {
                    File.Copy(exePath, backupPath, false);
                }
                catch
                {
                }
            }

            ushort updatedCharacteristics = (ushort)(characteristics | IMAGE_FILE_LARGE_ADDRESS_AWARE);
            stream.Seek(characteristicsOffset, SeekOrigin.Begin);
            writer.Write(updatedCharacteristics);
            writer.Flush();

            statusMessage = $"Enabled IMAGE_FILE_LARGE_ADDRESS_AWARE (0x0020). Virtual memory address space expanded to 4GB.";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"PE header modification error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks whether an executable has the IMAGE_FILE_LARGE_ADDRESS_AWARE flag set.
    /// </summary>
    public static bool IsLargeAddressAware(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return false;

        try
        {
            using var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 0x40) return false;

            ushort dosMagic = reader.ReadUInt16();
            if (dosMagic != 0x5A4D) return false;

            stream.Seek(0x3C, SeekOrigin.Begin);
            uint peOffset = reader.ReadUInt32();

            if (peOffset + 24 > stream.Length) return false;

            stream.Seek(peOffset, SeekOrigin.Begin);
            uint peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550) return false;

            stream.Seek(peOffset + 22, SeekOrigin.Begin);
            ushort characteristics = reader.ReadUInt16();

            return (characteristics & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0;
        }
        catch
        {
            return false;
        }
    }
}
