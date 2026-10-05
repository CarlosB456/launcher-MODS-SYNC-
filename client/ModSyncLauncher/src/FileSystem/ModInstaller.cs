using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ModSyncLauncher.Core;

namespace ModSyncLauncher.FileSystem;

public class ActiveCleoSession
{
    public string ActiveServerId { get; set; } = string.Empty;
    public List<string> StagedFiles { get; set; } = new();
    public List<string> BackedUpFiles { get; set; } = new();
}

public class ModInstaller
{
    private readonly string _gtaPath;
    private string _serverId;

    public string ServerId
    {
        get => _serverId;
        set => _serverId = SanitizeServerId(value);
    }

    public string ServerModloaderFolderName => Path.Combine("servers", ServerId);

    public ModInstaller(string gtaPath, string serverId = "openmp_server")
    {
        _gtaPath = gtaPath;
        _serverId = SanitizeServerId(serverId);
        EnsureModLoaderStructure();
    }

    public static string SanitizeServerId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "openmp_server";
        var sb = new StringBuilder();
        foreach (var ch in id)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                sb.Append(char.ToLowerInvariant(ch));
            else if (ch == ' ' || ch == '.' || ch == ':')
                sb.Append('_');
        }
        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "openmp_server" : result;
    }

    private void EnsureModLoaderStructure()
    {
        try
        {
            var modloaderDir = Path.Combine(_gtaPath, "modloader");
            var modloaderServersDir = Path.Combine(modloaderDir, "servers");
            var cleoServersDir = Path.Combine(_gtaPath, "cleo", "servers");

            if (!Directory.Exists(modloaderDir)) Directory.CreateDirectory(modloaderDir);
            if (!Directory.Exists(modloaderServersDir)) Directory.CreateDirectory(modloaderServersDir);
            if (!Directory.Exists(cleoServersDir)) Directory.CreateDirectory(cleoServersDir);

            // Clean obsolete legacy sync directories
            var legacyOpenmpSync = Path.Combine(modloaderDir, "openmp_sync");
            if (Directory.Exists(legacyOpenmpSync))
            {
                try { Directory.Delete(legacyOpenmpSync, true); } catch { }
            }

            // Remove any duplicate modloader.asi copies that prevent proper initialization.
            // ModLoader must only exist as modloader.asi in the GTA root, loaded once by the ASI Loader.
            var duplicateInModloader = Path.Combine(modloaderDir, "modloader.asi");
            if (File.Exists(duplicateInModloader))
            {
                try { File.Delete(duplicateInModloader); } catch { }
            }
            var scriptsDir = Path.Combine(_gtaPath, "scripts");
            var duplicateInScripts = Path.Combine(scriptsDir, "modloader.asi");
            if (File.Exists(duplicateInScripts))
            {
                try { File.Delete(duplicateInScripts); } catch { }
            }

            // Remove duplicate Ultimate ASI Loader (dinput8.dll) if present alongside vorbisFile.dll.
            var duplicateDinput8 = Path.Combine(_gtaPath, "dinput8.dll");
            if (File.Exists(duplicateDinput8))
            {
                try { File.Delete(duplicateDinput8); } catch { }
            }

            // Ensure modloader/.data/config.ini has EnableMenu = false to prevent Fatal error on ControllerBlockManager
            var configIni = Path.Combine(modloaderDir, ".data", "config.ini");
            if (File.Exists(configIni))
            {
                try
                {
                    var text = File.ReadAllText(configIni);
                    if (text.Contains("EnableMenu = true") || text.Contains("EnableMenu        = true"))
                    {
                        text = text.Replace("EnableMenu        = true", "EnableMenu        = false")
                                   .Replace("EnableMenu = true", "EnableMenu = false");
                        File.WriteAllText(configIni, text);
                    }
                }
                catch { }
            }

            // Restore original instruction in modloader.asi at 0x9D00 if previously patched to 0xC3
            var mlAsi = Path.Combine(_gtaPath, "modloader.asi");
            if (File.Exists(mlAsi))
            {
                try
                {
                    var bytes = File.ReadAllBytes(mlAsi);
                    if (bytes.Length > 0x9D05 && bytes[0x9D00] == 0xC3 && bytes[0x9D01] == 0x10)
                    {
                        bytes[0x9D00] = 0x6A;
                        File.WriteAllBytes(mlAsi, bytes);
                    }
                }
                catch { }
            }

            // Ensure CheckForDuplicateProcess bypass is applied to gta_sa.exe binary
            var gtaExe = Path.Combine(_gtaPath, "gta_sa.exe");
            if (File.Exists(gtaExe))
            {
                try
                {
                    var bytes = File.ReadAllBytes(gtaExe);
                    bool modified = false;
                    byte[] patch = [0x31, 0xC0, 0xC3, 0x90, 0x90];
                    if (bytes.Length > 0x00345CE5 && bytes[0x00345CE0] == 0xA1)
                    {
                        Buffer.BlockCopy(patch, 0, bytes, 0x00345CE0, patch.Length);
                        modified = true;
                    }
                    if (bytes.Length > 0x003468E5 && (bytes[0x003468E0] == 0x35 || bytes[0x003468E0] == 0xA1))
                    {
                        Buffer.BlockCopy(patch, 0, bytes, 0x003468E0, patch.Length);
                        modified = true;
                    }
                    if (modified)
                    {
                        File.WriteAllBytes(gtaExe, bytes);
                    }
                }
                catch { }
            }
        }
        catch
        {
        }
    }

    public bool InstallFile(ModManifestItem mod, ModFileItem file, string sourceFilePath)
    {
        try
        {
            var destinationPath = GetDestinationPath(mod, file);
            if (string.IsNullOrEmpty(destinationPath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Copy(sourceFilePath, destinationPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Install Error] Failed to install {file.Path} for mod {mod.Name}: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    public string GetDestinationPath(ModManifestItem mod, ModFileItem file)
    {
        var rawPath = (file.Path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        var fileName = Path.GetFileName(rawPath);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // 1. Universal CLEO support organized per server
        if (mod.Type == ModType.Script || rawPath.StartsWith("cleo/", StringComparison.OrdinalIgnoreCase) || ext is ".cs" or ".js" or ".cleo" or ".fxt" or ".ini")
        {
            string subCategory;
            string innerRelPath;

            if (rawPath.StartsWith("cleo/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    subCategory = parts[1].ToLowerInvariant();
                    innerRelPath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(2));
                }
                else
                {
                    subCategory = ext switch
                    {
                        ".cs" or ".js" => "scripts",
                        ".cleo" => "plugins",
                        ".fxt" => "text",
                        ".ini" => "config",
                        ".wav" or ".mp3" => "audio",
                        _ => "scripts"
                    };
                    innerRelPath = fileName;
                }
            }
            else
            {
                subCategory = ext switch
                {
                    ".cs" or ".js" => "scripts",
                    ".cleo" => "plugins",
                    ".fxt" => "text",
                    ".ini" => "config",
                    ".wav" or ".mp3" => "audio",
                    _ => "scripts"
                };
                innerRelPath = fileName;
            }

            return Path.Combine(_gtaPath, "cleo", "servers", ServerId, subCategory, innerRelPath);
        }

        // 2. Plugins (ASI) - Direct 1-level folder in modloader for clean profile loading
        if (mod.Type == ModType.Plugin || ext == ".asi")
        {
            return Path.Combine(_gtaPath, "modloader", ServerModloaderFolderName, mod.Id, fileName);
        }

        // 3. Models, Skins, Vehicles, Weapons, Objects, Textures, Audio organized per server
        // Preserve relative directory hierarchy inside mod folder (e.g., data/vehicles.ide)
        string modInnerPath = fileName;
        var modParts = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (modParts.Length > 1)
        {
            // Skip the top category folder (e.g., vehicles/, weapons/) and preserve nested relative path
            modInnerPath = string.Join(Path.DirectorySeparatorChar.ToString(), modParts.Skip(1));
        }

        return Path.Combine(_gtaPath, "modloader", ServerModloaderFolderName, mod.Id, modInnerPath);
    }

    public void ActivateServerSession(string? targetServerId = null)
    {
        if (!string.IsNullOrWhiteSpace(targetServerId))
        {
            ServerId = targetServerId;
        }

        var serverModloaderDir = Path.Combine(_gtaPath, "modloader", ServerModloaderFolderName);
        if (!Directory.Exists(serverModloaderDir)) Directory.CreateDirectory(serverModloaderDir);

        var serverCleoDir = Path.Combine(_gtaPath, "cleo", "servers", ServerId);
        if (!Directory.Exists(serverCleoDir)) Directory.CreateDirectory(serverCleoDir);

        // Ensure 4GB LAA flag and 2048MB streaming memory configuration
        var gtaExe = Path.Combine(_gtaPath, "gta_sa.exe");
        if (File.Exists(gtaExe))
        {
            PeHeaderModifier.EnsureLargeAddressAware(gtaExe, out _);
        }
        ConfigureStreamingMemory();

        ActivateModLoaderServerProfile();
        ActivateCleoServerSession();
        SynchronizeImgArchive();
    }

    public void DeactivateSession()
    {
        DeactivateModLoaderServerProfile();
        DeactivateCleoServerSession();
        RestoreImgArchive();
    }

    private void ActivateModLoaderServerProfile()
    {
        try
        {
            var modloaderDir = Path.Combine(_gtaPath, "modloader");
            var modloaderIniPath = Path.Combine(modloaderDir, "modloader.ini");
            var activeServerFolder = $"servers/{ServerId}";

            // Enumerate all servers/* folders inside modloader
            var serversBase = Path.Combine(modloaderDir, "servers");
            var existingServerFolders = Directory.Exists(serversBase)
                ? Directory.GetDirectories(serversBase)
                    .Select(Path.GetFileName)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => $"servers/{s}")
                    .ToList()
                : new List<string>();

            List<string> lines;
            if (File.Exists(modloaderIniPath))
            {
                lines = File.ReadAllLines(modloaderIniPath).ToList();
            }
            else
            {
                lines = new List<string>
                {
                    "; Mod Loader Folder Config File",
                    "[Folder.Config]",
                    "Profile = Default",
                    "PriorityLimit = 100",
                    "",
                    "[Profiles.Default.Config]",
                    "IgnoreAllMods = false",
                    "ExcludeAllMods = false",
                    "",
                    "[Profiles.Default.Priority]",
                    "",
                    "[Profiles.Default.IgnoreMods]",
                    "_ignore"
                };
            }

            // Ensure priority and ignore sections exist
            bool hasPrioritySection = lines.Any(l => l.Trim().StartsWith("[Profiles.Default.Priority]", StringComparison.OrdinalIgnoreCase));
            bool hasIgnoreSection = lines.Any(l => l.Trim().StartsWith("[Profiles.Default.IgnoreMods]", StringComparison.OrdinalIgnoreCase));

            if (!hasPrioritySection)
            {
                lines.Add("");
                lines.Add("[Profiles.Default.Priority]");
            }
            if (!hasIgnoreSection)
            {
                lines.Add("");
                lines.Add("[Profiles.Default.IgnoreMods]");
                lines.Add("_ignore");
            }

            var newLines = new List<string>();
            bool inPrioritySection = false;
            bool inIgnoreSection = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("[Profiles.Default.Priority]", StringComparison.OrdinalIgnoreCase))
                {
                    inPrioritySection = true;
                    inIgnoreSection = false;
                    newLines.Add(lines[i]);
                    newLines.Add($"{activeServerFolder}=90");
                    continue;
                }

                if (line.StartsWith("[Profiles.Default.IgnoreMods]", StringComparison.OrdinalIgnoreCase))
                {
                    inIgnoreSection = true;
                    inPrioritySection = false;
                    newLines.Add(lines[i]);
                    newLines.Add("_ignore");
                    foreach (var otherServer in existingServerFolders)
                    {
                        if (!string.IsNullOrEmpty(otherServer) && !string.Equals(otherServer, activeServerFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            newLines.Add(otherServer);
                        }
                    }
                    continue;
                }

                if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase) && line.EndsWith("]"))
                {
                    inPrioritySection = false;
                    inIgnoreSection = false;
                }

                if (inPrioritySection && (line.StartsWith("openmp_", StringComparison.OrdinalIgnoreCase) || line.StartsWith("servers/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // Skip old server priority entries
                }

                if (inIgnoreSection && (line.StartsWith("openmp_", StringComparison.OrdinalIgnoreCase) || line.StartsWith("servers/", StringComparison.OrdinalIgnoreCase) || line == "_ignore"))
                {
                    continue; // Skip old ignore entries
                }

                newLines.Add(lines[i]);
            }

            File.WriteAllLines(modloaderIniPath, newLines, Encoding.UTF8);
            var rootIniPath = Path.Combine(_gtaPath, "modloader.ini");
            try { File.WriteAllLines(rootIniPath, newLines, Encoding.UTF8); } catch { }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModLoader Notice] Could not update active server profile: {ex.Message}");
        }
    }

    private void DeactivateModLoaderServerProfile()
    {
        try
        {
            var modloaderIniPath = Path.Combine(_gtaPath, "modloader", "modloader.ini");
            if (!File.Exists(modloaderIniPath)) return;

            var lines = File.ReadAllLines(modloaderIniPath).ToList();
            var newLines = new List<string>();
            bool inPrioritySection = false;
            bool inIgnoreSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("[Profiles.Default.Priority]", StringComparison.OrdinalIgnoreCase))
                {
                    inPrioritySection = true;
                    inIgnoreSection = false;
                    newLines.Add(line);
                    continue;
                }

                if (trimmed.StartsWith("[Profiles.Default.IgnoreMods]", StringComparison.OrdinalIgnoreCase))
                {
                    inIgnoreSection = true;
                    inPrioritySection = false;
                    newLines.Add(line);
                    newLines.Add("_ignore");
                    continue;
                }

                if (trimmed.StartsWith("[", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]"))
                {
                    inPrioritySection = false;
                    inIgnoreSection = false;
                }

                if (inPrioritySection && (trimmed.StartsWith("openmp_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("servers/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (inIgnoreSection && (trimmed.StartsWith("openmp_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("servers/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                newLines.Add(line);
            }

            File.WriteAllLines(modloaderIniPath, newLines, Encoding.UTF8);
            var rootIniPath = Path.Combine(_gtaPath, "modloader.ini");
            try { File.WriteAllLines(rootIniPath, newLines, Encoding.UTF8); } catch { }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModLoader Notice] Could not reset modloader server profile: {ex.Message}");
        }
    }

    private void ActivateCleoServerSession()
    {
        try
        {
            var cleoRoot = Path.Combine(_gtaPath, "cleo");
            var serverCleoDir = Path.Combine(cleoRoot, "servers", ServerId);
            var sessionTrackerPath = Path.Combine(cleoRoot, ".openmp_active_session.json");
            var backupDir = Path.Combine(cleoRoot, ".openmp_session_backups");

            // 1. Clean previous staged files and restore any personal backups
            DeactivateCleoServerSession();

            var currentStagedFiles = new List<string>();
            var currentBackedUpFiles = new List<string>();

            if (Directory.Exists(serverCleoDir))
            {
                void SafeStageFile(string sourceFilePath, string targetFilePath, string stagedRelPath)
                {
                    if (File.Exists(targetFilePath))
                    {
                        var targetDir = Path.GetDirectoryName(targetFilePath) ?? string.Empty;
                        var relToGta = Path.GetRelativePath(_gtaPath, targetFilePath);
                        var backupTarget = Path.Combine(backupDir, relToGta);
                        var backupSubDir = Path.GetDirectoryName(backupTarget);
                        if (!string.IsNullOrEmpty(backupSubDir) && !Directory.Exists(backupSubDir))
                        {
                            Directory.CreateDirectory(backupSubDir);
                        }

                        if (!File.Exists(backupTarget))
                        {
                            File.Copy(targetFilePath, backupTarget, overwrite: true);
                            currentBackedUpFiles.Add(relToGta);
                        }
                    }
                    else
                    {
                        var destDir = Path.GetDirectoryName(targetFilePath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                    }

                    File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                    currentStagedFiles.Add(stagedRelPath);
                }

                // Scripts (.cs, .js) staged directly into cleo/ with server prefix
                var scriptsDir = Path.Combine(serverCleoDir, "scripts");
                if (Directory.Exists(scriptsDir))
                {
                    foreach (var scriptFile in Directory.GetFiles(scriptsDir, "*.*", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileName(scriptFile);
                        var stagedName = $"openmp_{ServerId}_{fileName}";
                        var targetPath = Path.Combine(cleoRoot, stagedName);
                        SafeStageFile(scriptFile, targetPath, Path.Combine("cleo", stagedName));
                    }
                }

                // Plugins (.cleo) staged into cleo/cleo_plugins/ and cleo/
                var pluginsDir = Path.Combine(serverCleoDir, "plugins");
                if (Directory.Exists(pluginsDir))
                {
                    var destPluginsDir = Path.Combine(cleoRoot, "cleo_plugins");
                    foreach (var pluginFile in Directory.GetFiles(pluginsDir, "*.*", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileName(pluginFile);
                        var targetPath = Path.Combine(destPluginsDir, fileName);
                        SafeStageFile(pluginFile, targetPath, Path.Combine("cleo", "cleo_plugins", fileName));
                    }
                }

                // Text (.fxt) staged into cleo/cleo_text/
                var textDir = Path.Combine(serverCleoDir, "text");
                if (Directory.Exists(textDir))
                {
                    var destTextDir = Path.Combine(cleoRoot, "cleo_text");
                    foreach (var textFile in Directory.GetFiles(textDir, "*.*", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileName(textFile);
                        var targetPath = Path.Combine(destTextDir, fileName);
                        SafeStageFile(textFile, targetPath, Path.Combine("cleo", "cleo_text", fileName));
                    }
                }

                // Config (.ini) staged into cleo/
                var configDir = Path.Combine(serverCleoDir, "config");
                if (Directory.Exists(configDir))
                {
                    foreach (var configFile in Directory.GetFiles(configDir, "*.*", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileName(configFile);
                        var targetPath = Path.Combine(cleoRoot, fileName);
                        SafeStageFile(configFile, targetPath, Path.Combine("cleo", fileName));
                    }
                }

                // Audio (.wav, .mp3) staged into cleo/cleo_audio/
                var audioDir = Path.Combine(serverCleoDir, "audio");
                if (Directory.Exists(audioDir))
                {
                    var destAudioDir = Path.Combine(cleoRoot, "cleo_audio");
                    foreach (var audioFile in Directory.GetFiles(audioDir, "*.*", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileName(audioFile);
                        var targetPath = Path.Combine(destAudioDir, fileName);
                        SafeStageFile(audioFile, targetPath, Path.Combine("cleo", "cleo_audio", fileName));
                    }
                }
            }

            var newSession = new ActiveCleoSession
            {
                ActiveServerId = ServerId,
                StagedFiles = currentStagedFiles,
                BackedUpFiles = currentBackedUpFiles
            };
            File.WriteAllText(sessionTrackerPath, JsonSerializer.Serialize(newSession, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEO Notice] Could not stage server CLEO session: {ex.Message}");
        }
    }

    private void DeactivateCleoServerSession()
    {
        try
        {
            var cleoRoot = Path.Combine(_gtaPath, "cleo");
            var sessionTrackerPath = Path.Combine(cleoRoot, ".openmp_active_session.json");
            var backupDir = Path.Combine(cleoRoot, ".openmp_session_backups");

            if (!File.Exists(sessionTrackerPath)) return;

            var json = File.ReadAllText(sessionTrackerPath);
            var oldSession = JsonSerializer.Deserialize<ActiveCleoSession>(json);

            if (oldSession != null)
            {
                if (oldSession.StagedFiles != null)
                {
                    foreach (var stagedRel in oldSession.StagedFiles)
                    {
                        var fullPath = Path.Combine(_gtaPath, stagedRel);
                        if (File.Exists(fullPath))
                        {
                            try { File.Delete(fullPath); } catch { }
                        }
                    }
                }

                if (oldSession.BackedUpFiles != null)
                {
                    foreach (var backedUpRel in oldSession.BackedUpFiles)
                    {
                        var backupSource = Path.Combine(backupDir, backedUpRel);
                        var restoreDest = Path.Combine(_gtaPath, backedUpRel);

                        if (File.Exists(backupSource))
                        {
                            try
                            {
                                var destDir = Path.GetDirectoryName(restoreDest);
                                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                                {
                                    Directory.CreateDirectory(destDir);
                                }
                                File.Copy(backupSource, restoreDest, overwrite: true);
                                File.Delete(backupSource);
                            }
                            catch { }
                        }
                    }
                }
            }

            if (Directory.Exists(backupDir))
            {
                try { Directory.Delete(backupDir, true); } catch { }
            }

            try { File.Delete(sessionTrackerPath); } catch { }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEO Notice] Could not deactivate previous CLEO session: {ex.Message}");
        }
    }

    public void SynchronizeImgArchive()
    {
        try
        {
            var modelsDir = Path.Combine(_gtaPath, "models");
            var imgPath = Path.Combine(modelsDir, "gta3.img");
            var backupPath = Path.Combine(modelsDir, "gta3.img.bak");

            if (!File.Exists(imgPath)) return;

            // Ensure streaming memory is set to 2048MB budget (2096128 KB) and 96 vehicles in stream.ini
            ConfigureStreamingMemory();

            var modloaderDir = Path.Combine(_gtaPath, "modloader", ServerModloaderFolderName);
            if (!Directory.Exists(modloaderDir)) return;

            var modFiles = Directory.GetFiles(modloaderDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".dff", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txd", StringComparison.OrdinalIgnoreCase))
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (modFiles.Count == 0) return;

            // Ensure pristine backup exists
            if (!File.Exists(backupPath))
            {
                try
                {
                    File.Copy(imgPath, backupPath, false);
                    Console.WriteLine("[IMG Sync] Created backup models/gta3.img.bak");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IMG Sync] Backup notice: {ex.Message}");
                }
            }

            using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            using var br = new BinaryReader(fs);
            using var bw = new BinaryWriter(fs);

            byte[] header = br.ReadBytes(8);
            if (header.Length < 8) return;
            string magic = Encoding.ASCII.GetString(header, 0, 4);
            if (magic != "VER2") return;

            uint numEntries = BitConverter.ToUInt32(header, 4);
            var entries = new Dictionary<string, (int index, uint offset, uint sizeBlocks)>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < numEntries; i++)
            {
                long pos = 8 + i * 32;
                fs.Seek(pos, SeekOrigin.Begin);
                uint offset = br.ReadUInt32();
                uint sizeBlocks = br.ReadUInt32();
                byte[] nameBytes = br.ReadBytes(24);
                string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                if (!string.IsNullOrEmpty(name))
                {
                    entries[name] = (i, offset, sizeBlocks);
                }
            }

            fs.Seek(0, SeekOrigin.End);
            long fileLength = fs.Position;
            uint currentEofBlock = (uint)Math.Ceiling((double)fileLength / 2048.0);
            long alignedLength = (long)currentEofBlock * 2048;
            if (fileLength < alignedLength)
            {
                bw.Write(new byte[alignedLength - fileLength]);
            }

            int syncedCount = 0;
            foreach (var modFile in modFiles)
            {
                string fileName = Path.GetFileName(modFile);
                if (entries.TryGetValue(fileName, out var entryInfo))
                {
                    byte[] data = File.ReadAllBytes(modFile);
                    uint neededBlocks = (uint)Math.Ceiling((double)data.Length / 2048.0);

                    if (entryInfo.sizeBlocks == neededBlocks)
                    {
                        fs.Seek((long)entryInfo.offset * 2048, SeekOrigin.Begin);
                        byte[] existingSample = br.ReadBytes(Math.Min(128, data.Length));
                        bool match = true;
                        for (int k = 0; k < existingSample.Length; k++)
                        {
                            if (existingSample[k] != data[k]) { match = false; break; }
                        }
                        if (match) continue;
                    }

                    byte[] padded = new byte[neededBlocks * 2048];
                    Buffer.BlockCopy(data, 0, padded, 0, data.Length);

                    uint targetOffsetBlock;
                    if (neededBlocks <= entryInfo.sizeBlocks)
                    {
                        targetOffsetBlock = entryInfo.offset;
                        fs.Seek((long)targetOffsetBlock * 2048, SeekOrigin.Begin);
                        bw.Write(padded);

                        fs.Seek(8 + entryInfo.index * 32 + 4, SeekOrigin.Begin);
                        bw.Write(neededBlocks);
                    }
                    else
                    {
                        targetOffsetBlock = currentEofBlock;
                        fs.Seek((long)targetOffsetBlock * 2048, SeekOrigin.Begin);
                        bw.Write(padded);
                        currentEofBlock += neededBlocks;

                        fs.Seek(8 + entryInfo.index * 32, SeekOrigin.Begin);
                        bw.Write(targetOffsetBlock);
                        bw.Write(neededBlocks);
                    }

                    syncedCount++;
                }
            }

            if (syncedCount > 0)
            {
                Console.WriteLine($"[IMG Sync] Synchronized {syncedCount} model/texture assets into models/gta3.img.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IMG Sync Notice] {ex.Message}");
        }
    }

    public void RestoreImgArchive()
    {
        try
        {
            var modelsDir = Path.Combine(_gtaPath, "models");
            var imgPath = Path.Combine(modelsDir, "gta3.img");
            var backupPath = Path.Combine(modelsDir, "gta3.img.bak");

            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, imgPath, overwrite: true);
                Console.WriteLine("[IMG Sync] Restored models/gta3.img from vanilla backup.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IMG Sync Notice] Could not restore gta3.img: {ex.Message}");
        }
    }

    public void ConfigureStreamingMemory()
    {
        try
        {
            var streamIni = Path.Combine(_gtaPath, "stream.ini");
            const string optimalMemoryKb = "2096128"; // 2047MB (0x7FE00000 bytes) - 2GB safe ceiling avoiding signed int overflow
            const string optimalVehicles = "96";      // Boosted vehicle streaming pool capacity

            if (File.Exists(streamIni))
            {
                var lines = File.ReadAllLines(streamIni).ToList();
                bool memoryFound = false;
                bool devkitFound = false;
                bool vehiclesFound = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("memory", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("devkit_memory", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"memory\t\t{optimalMemoryKb}";
                        memoryFound = true;
                    }
                    else if (line.StartsWith("devkit_memory", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"devkit_memory\t{optimalMemoryKb}";
                        devkitFound = true;
                    }
                    else if (line.StartsWith("vehicles", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"vehicles\t{optimalVehicles}";
                        vehiclesFound = true;
                    }
                }

                if (!memoryFound) lines.Insert(0, $"memory\t\t{optimalMemoryKb}");
                if (!devkitFound) lines.Insert(1, $"devkit_memory\t{optimalMemoryKb}");
                if (!vehiclesFound) lines.Insert(2, $"vehicles\t{optimalVehicles}");

                File.WriteAllLines(streamIni, lines);
            }
            else
            {
                File.WriteAllText(streamIni, $"memory\t\t{optimalMemoryKb}\r\ndevkit_memory\t{optimalMemoryKb}\r\nvehicles\t{optimalVehicles}\r\npe_lightchangerate\t0.0005\r\npe_lightingbasecap\t0.35\r\npe_lightingbasemult\t0.5\r\npe_leftx\t16\r\npe_topy\t\t16\r\npe_rightx\t16\r\npe_bottomy\t16\r\ndontbuildpaths\r\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STREAMING] Notice configuring stream.ini: {ex.Message}");
        }
    }
}
