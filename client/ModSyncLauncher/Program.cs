using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModSyncLauncher.Core;
using ModSyncLauncher.FileSystem;
using ModSyncLauncher.Network;
using ModSyncLauncher.Sync;
using ModSyncLauncher.UI;

namespace ModSyncLauncher;

public class Program
{
    private const string Version = "0.4.0 - R1";
    private const string Credits = "eLdarqO";

    public static async Task<int> Main(string[] args)
    {
        var config = ParseArgs(args);

        if (string.IsNullOrEmpty(config.GtaPath) || !GtaPathResolver.IsValidGtaPath(config.GtaPath))
        {
            config.GtaPath = GtaPathResolver.FindGtaPath() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(config.GtaPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Could not find GTA San Andreas installation directory.");
            Console.WriteLine("Please specify it manually using --gta-path <path>");
            Console.ResetColor();
            return 1;
        }

        // Handle session deactivation / clean reset
        if (config.DeactivateSession)
        {
            var cleanInstaller = new ModInstaller(config.GtaPath, config.ServerId);
            cleanInstaller.DeactivateSession();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO] ModSync v{Version} (Credits: {Credits})");
            Console.WriteLine("[INFO] Active server mod session deactivated successfully. Game restored to vanilla state.");
            Console.ResetColor();
            return 0;
        }

        if (string.IsNullOrEmpty(config.ServerAddress))
        {
            var localInstaller = new ModInstaller(config.GtaPath, config.ServerId);
            localInstaller.ActivateServerSession();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO] open.mp ModSync v{Version} (Credits: {Credits})");
            Console.WriteLine($"[INFO] Active mod profile '{localInstaller.ServerModloaderFolderName}' initialized.");
            Console.ResetColor();

            if (config.SyncOnly)
            {
                Console.WriteLine("[INFO] Archives and mod profile synchronized successfully. Ready to play.");
                return 0;
            }

            LaunchGame(config, localInstaller);
            return 0;
        }

        // Auto-infer host from server address if default
        if (!string.IsNullOrEmpty(config.ServerAddress) && config.ServerHost == "127.0.0.1")
        {
            try
            {
                var uri = new Uri(config.ServerAddress);
                if (!string.IsNullOrEmpty(uri.Host) && uri.Host != "127.0.0.1" && uri.Host != "localhost")
                {
                    config.ServerHost = uri.Host;
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(config.CacheDirectory))
        {
            config.CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenMpModSync", "Cache");
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("\nCancelling...");
            cts.Cancel();
            e.Cancel = true;
        };

        var renderer = new ConsoleRenderer();
        using var httpClient = new HttpClient();

        using var manifestClient = new ManifestClient(httpClient, config);
        using var downloader = new ModDownloader(httpClient);
        var cacheManager = new CacheManager(config.CacheDirectory);
        var installer = new ModInstaller(config.GtaPath, config.ServerId);

        var engine = new SyncEngine(config, manifestClient, downloader, cacheManager, installer);

        engine.OnManifestLoaded += (manifest) => renderer.DisplayHeader(manifest.DisplayName, manifest.EffectiveServerId, manifest.Mods.Count);
        engine.OnSyncStatusChanged += renderer.UpdateStatus;
        engine.OnModDownloadProgress += renderer.UpdateProgress;
        engine.OnModProcessed += renderer.ModProcessed;

        var result = await engine.RunSyncAsync(cts.Token);

        renderer.DisplaySummary(result);

        if (result.Status == SyncStatus.Success && config.AutoLaunchGame)
        {
            LaunchGame(config, installer);
        }

        return result.Status == SyncStatus.Success ? 0 : 1;
    }

    private static ModSyncConfig ParseArgs(string[] args)
    {
        var config = new ModSyncConfig();
        LoadConfigFile(config);

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--server":
                    if (i + 1 < args.Length) config.ServerAddress = args[++i];
                    break;
                case "--server-id":
                    if (i + 1 < args.Length) config.ServerId = args[++i];
                    break;
                case "--gta-path":
                    if (i + 1 < args.Length) config.GtaPath = args[++i];
                    break;
                case "--cache-dir":
                    if (i + 1 < args.Length) config.CacheDirectory = args[++i];
                    break;
                case "--cdn-url":
                    if (i + 1 < args.Length) config.CdnUrl = args[++i];
                    break;
                case "--max-downloads":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int max))
                        config.MaxConcurrentDownloads = max;
                    break;
                case "--launch":
                case "--auto-launch":
                    config.AutoLaunchGame = true;
                    break;
                case "--api-key":
                    if (i + 1 < args.Length) config.ApiKey = args[++i];
                    break;
                case "--player-name":
                case "-n":
                    if (i + 1 < args.Length) config.PlayerName = args[++i];
                    break;
                case "--host":
                case "-h":
                    if (i + 1 < args.Length) config.ServerHost = args[++i];
                    break;
                case "--port":
                case "-p":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                        config.ServerPort = port;
                    break;
                case "--clean":
                case "--reset":
                case "--deactivate":
                    config.DeactivateSession = true;
                    break;
                case "--clean-on-exit":
                case "--wait-game":
                    config.CleanOnExit = true;
                    break;
                case "--sync-only":
                case "--setup":
                    config.SyncOnly = true;
                    break;
            }
        }

        return config;
    }

    private static void LoadConfigFile(ModSyncConfig config)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "launcher_config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "launcher_config.json"),
            Path.Combine(AppContext.BaseDirectory, "modsync.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "modsync.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("server_address", out var sa)) config.ServerAddress = sa.GetString() ?? config.ServerAddress;
                    if (root.TryGetProperty("server_id", out var sid)) config.ServerId = sid.GetString() ?? config.ServerId;
                    if (root.TryGetProperty("server_host", out var sh)) config.ServerHost = sh.GetString() ?? config.ServerHost;
                    if (root.TryGetProperty("server_port", out var sp) && sp.TryGetInt32(out var port)) config.ServerPort = port;
                    if (root.TryGetProperty("player_name", out var pn)) config.PlayerName = pn.GetString() ?? config.PlayerName;
                    if (root.TryGetProperty("auto_launch", out var al)) config.AutoLaunchGame = al.GetBoolean();
                    if (root.TryGetProperty("gta_path", out var gp)) config.GtaPath = gp.GetString() ?? config.GtaPath;
                    break;
                }
                catch { }
            }
        }
    }

    private static void LaunchGame(ModSyncConfig config, ModInstaller installer)
    {
        try
        {
            var gtaExe = Path.Combine(config.GtaPath, "gta_sa.exe");
            var sampExe = Path.Combine(config.GtaPath, "samp.exe");
            var sampCmd = Path.Combine(config.GtaPath, "sampcmd.exe");
            var sampDll = Path.Combine(config.GtaPath, "samp.dll");

            var playerName = (config.PlayerName ?? "ModSyncPlayer").Trim().Trim('"').Trim('\'');
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "ModSyncPlayer";

            // 1. Prime SA-MP Registry settings
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\SAMP");
                    if (key != null)
                    {
                        key.SetValue("PlayerName", playerName);
                        key.SetValue("gta_sa_exe", gtaExe);
                    }
                }
                catch (Exception regEx)
                {
                    Console.WriteLine($"[REGISTRY] Notice: {regEx.Message}");
                }
            }

            // 2. Ensure sampcmd.exe is present in GTA folder if available in launcher paths
            if (!File.Exists(sampCmd))
            {
                var candidateLocations = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "sampcmd.exe"),
                    Path.Combine(Directory.GetCurrentDirectory(), "sampcmd.exe"),
                    Path.Combine(Directory.GetCurrentDirectory(), "client", "ModSyncLauncher", "sampcmd.exe")
                };

                foreach (var loc in candidateLocations)
                {
                    if (File.Exists(loc))
                    {
                        File.Copy(loc, sampCmd, true);
                        break;
                    }
                }
            }

            // 3. Priority A: Direct SA-MP process injector (sampcmd.exe)
            if (File.Exists(sampCmd) && File.Exists(sampDll))
            {
                Console.WriteLine($"[MULTIPLAYER] Launching open.mp multiplayer via injector ({config.ServerHost}:{config.ServerPort} - {playerName})...");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = sampCmd,
                    Arguments = $"-c -h {config.ServerHost} -p {config.ServerPort} -n {playerName}",
                    WorkingDirectory = config.GtaPath,
                    UseShellExecute = true
                });

                if (config.CleanOnExit && process != null)
                {
                    WaitForGameExitAndCleanup(installer);
                }
                return;
            }

            // 4. Priority B: Official SA-MP client launcher (samp.exe)
            if (File.Exists(sampExe))
            {
                Console.WriteLine($"[MULTIPLAYER] Launching via official SA-MP client ({config.ServerHost}:{config.ServerPort} - {config.PlayerName})...");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = sampExe,
                    Arguments = $"{config.ServerHost} {config.ServerPort}",
                    WorkingDirectory = config.GtaPath,
                    UseShellExecute = true
                });

                if (config.CleanOnExit && process != null)
                {
                    WaitForGameExitAndCleanup(installer);
                }
                return;
            }

            // 5. Fallback warning
            if (File.Exists(gtaExe))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] samp.exe and sampcmd.exe not found in GTA directory. Starting base game...");
                Console.ResetColor();
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = gtaExe,
                    WorkingDirectory = config.GtaPath,
                    UseShellExecute = true
                });

                if (config.CleanOnExit && process != null)
                {
                    try
                    {
                        process.WaitForExit();
                        installer.DeactivateSession();
                        Console.WriteLine("[CLEANUP] Server session deactivated on game exit.");
                    }
                    catch { }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Game executable not found in: {config.GtaPath}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to launch multiplayer game: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void WaitForGameExitAndCleanup(ModInstaller installer)
    {
        try
        {
            Console.WriteLine("[WATCHDOG] Monitoring GTA San Andreas process for clean exit...");
            // Allow up to 10 seconds for gta_sa.exe to spawn
            Process? gtaProc = null;
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(500);
                var procs = Process.GetProcessesByName("gta_sa");
                if (procs.Length > 0)
                {
                    gtaProc = procs[0];
                    break;
                }
            }

            if (gtaProc != null)
            {
                gtaProc.WaitForExit();
                Thread.Sleep(500);
                installer.DeactivateSession();
                Console.WriteLine("[CLEANUP] Server session deactivated cleanly after game close.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WATCHDOG Notice] {ex.Message}");
        }
    }
}
