using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModSyncLauncher.Core;
using ModSyncLauncher.FileSystem;
using Xunit;

namespace ModSyncLauncher.Tests;

public class ModSyncTests : IDisposable
{
    private readonly string _tempDir;

    public ModSyncTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ModSyncTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void Test_Manifest_Deserialization_With_ServerId_And_Additions()
    {
        var json = """
        {
          "server_id": "chile_police_roleplay",
          "server_name": "open.mp Chile RP",
          "version": "0.3.8-A-R1",
          "author": "eLdarqO",
          "credits": "eLdarqO",
          "mods": [
            {
              "id": "veh_add_suv",
              "name": "Chilean SUV",
              "type": "vehicle",
              "base_model_id": 596,
              "new_model_id": 20050,
              "is_addition": true,
              "files": [
                {
                  "path": "vehicles/suv.dff",
                  "sha256": "abc123hash",
                  "size_bytes": 1024,
                  "type": "dff"
                }
              ]
            }
          ]
        }
        """;

        var manifest = JsonSerializer.Deserialize<ModManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("chile_police_roleplay", manifest.ServerId);
        Assert.Equal("chile_police_roleplay", manifest.EffectiveServerId);
        Assert.Equal("0.3.8-A-R1", manifest.Version);
        Assert.Equal("eLdarqO", manifest.Author);
        Assert.Equal("eLdarqO", manifest.Credits);
        Assert.Single(manifest.Mods);

        var mod = manifest.Mods[0];
        Assert.Equal("veh_add_suv", mod.Id);
        Assert.True(mod.IsAddition);
        Assert.Equal(596, mod.BaseModelId);
        Assert.Equal(20050, mod.NewModelId);
    }

    [Theory]
    [InlineData("My Server 2026!", "my_server_2026")]
    [InlineData("127.0.0.1:7777", "127_0_0_1_7777")]
    [InlineData("Chile Roleplay @ open.mp", "chile_roleplay_open_mp")]
    [InlineData("", "openmp_server")]
    public void Test_EffectiveServerId_Sanitization(string input, string expected)
    {
        var manifest = new ModManifest { ServerId = input };
        Assert.Equal(expected, manifest.EffectiveServerId);
    }

    [Fact]
    public void Test_ModInstaller_Server_Isolated_Destination_Paths()
    {
        var installer = new ModInstaller(_tempDir, "chile_police_roleplay");

        // 1. Vehicle model
        var vehMod = new ModManifestItem { Id = "veh_police_ls", Type = ModType.Vehicle };
        var vehFile = new ModFileItem { Path = "vehicles/copcarla.dff" };
        var vehPath = installer.GetDestinationPath(vehMod, vehFile);
        Assert.Contains(Path.Combine("modloader", "servers", "chile_police_roleplay", "veh_police_ls", "copcarla.dff"), vehPath);

        // 2. CLEO script (.cs classic)
        var csMod = new ModManifestItem { Id = "cleo_sync", Type = ModType.Script };
        var csFile = new ModFileItem { Path = "cleo/nitro.cs" };
        var csPath = installer.GetDestinationPath(csMod, csFile);
        Assert.Contains(Path.Combine("cleo", "servers", "chile_police_roleplay", "scripts", "nitro.cs"), csPath);

        // 3. CLEO Redux script (.js)
        var jsFile = new ModFileItem { Path = "cleo/spawner.js" };
        var jsPath = installer.GetDestinationPath(csMod, jsFile);
        Assert.Contains(Path.Combine("cleo", "servers", "chile_police_roleplay", "scripts", "spawner.js"), jsPath);

        // 4. CLEO plugin (.cleo)
        var cleoPlugFile = new ModFileItem { Path = "cleo/Events.cleo" };
        var cleoPlugPath = installer.GetDestinationPath(csMod, cleoPlugFile);
        Assert.Contains(Path.Combine("cleo", "servers", "chile_police_roleplay", "plugins", "Events.cleo"), cleoPlugPath);

        // 5. CLEO config (.ini)
        var iniFile = new ModFileItem { Path = "cleo/config.ini" };
        var iniPath = installer.GetDestinationPath(csMod, iniFile);
        Assert.Contains(Path.Combine("cleo", "servers", "chile_police_roleplay", "config", "config.ini"), iniPath);

        // 6. ASI Plugin (isolated inside modloader per server profile)
        var asiMod = new ModManifestItem { Id = "modsync_hook", Type = ModType.Plugin };
        var asiFile = new ModFileItem { Path = "plugins/modsync_hook.asi" };
        var asiPath = installer.GetDestinationPath(asiMod, asiFile);
        Assert.Contains(Path.Combine("modloader", "servers", "chile_police_roleplay", "modsync_hook", "modsync_hook.asi"), asiPath);
    }

    [Fact]
    public void Test_ModInstaller_Preserves_Gta3Img_And_Copies_To_ModLoader()
    {
        // Mock GTA directory structure with models/gta3.img
        var modelsDir = Path.Combine(_tempDir, "models");
        Directory.CreateDirectory(modelsDir);
        var gta3ImgPath = Path.Combine(modelsDir, "gta3.img");
        var originalContent = new byte[] { 0x56, 0x45, 0x52, 0x32, 0x00, 0x00, 0x00, 0x00 };
        File.WriteAllBytes(gta3ImgPath, originalContent);

        // Dummy mod file to install
        var sourceModFile = Path.Combine(_tempDir, "source_copcarla.dff");
        File.WriteAllText(sourceModFile, "dummy dff model content");

        var installer = new ModInstaller(_tempDir, "server_alpha");
        var mod = new ModManifestItem { Id = "veh_police", Type = ModType.Vehicle };
        var file = new ModFileItem { Path = "vehicles/copcarla.dff" };

        var ok = installer.InstallFile(mod, file, sourceModFile);
        Assert.True(ok);

        // Verify modloader server folder has the file
        var targetFile = Path.Combine(_tempDir, "modloader", "servers", "server_alpha", "veh_police", "copcarla.dff");
        Assert.True(File.Exists(targetFile));
        Assert.Equal("dummy dff model content", File.ReadAllText(targetFile));

        // Verify models/gta3.img remains 100% identical and untouched!
        var gta3Bytes = File.ReadAllBytes(gta3ImgPath);
        Assert.Equal(originalContent, gta3Bytes);
    }

    [Fact]
    public void Test_Server_Isolation_And_Switching()
    {
        // Setup mock modloader.ini
        var modloaderDir = Path.Combine(_tempDir, "modloader");
        Directory.CreateDirectory(modloaderDir);
        var iniContent = """
        [Profiles.Default.Priority]
        ; priorities

        [Profiles.Default.IgnoreMods]
        _ignore
        """;
        File.WriteAllText(Path.Combine(modloaderDir, "modloader.ini"), iniContent);

        // Setup mock CLEO root
        var cleoDir = Path.Combine(_tempDir, "cleo");
        Directory.CreateDirectory(cleoDir);
        var basePersonalScript = Path.Combine(cleoDir, "my_personal_mod.cs");
        File.WriteAllText(basePersonalScript, "personal script content");

        // 1. Install & Activate Server A
        var installerA = new ModInstaller(_tempDir, "server_alpha");
        var dummyScriptA = Path.Combine(_tempDir, "scriptA.js");
        File.WriteAllText(dummyScriptA, "console.log('Server A');");
        var modA = new ModManifestItem { Id = "scripts_a", Type = ModType.Script };
        var fileA = new ModFileItem { Path = "scripts/scriptA.js" };
        installerA.InstallFile(modA, fileA, dummyScriptA);
        installerA.ActivateServerSession("server_alpha");

        // Verify Server A script staged
        var stagedA = Path.Combine(cleoDir, "openmp_server_alpha_scriptA.js");
        Assert.True(File.Exists(stagedA));
        Assert.True(File.Exists(basePersonalScript)); // Personal script intact!

        // 2. Install & Activate Server B
        var installerB = new ModInstaller(_tempDir, "server_beta");
        var dummyScriptB = Path.Combine(_tempDir, "scriptB.cs");
        File.WriteAllText(dummyScriptB, "cleo script B content");
        var modB = new ModManifestItem { Id = "scripts_b", Type = ModType.Script };
        var fileB = new ModFileItem { Path = "scripts/scriptB.cs" };
        installerB.InstallFile(modB, fileB, dummyScriptB);
        installerB.ActivateServerSession("server_beta");

        // Verify Server A script was cleanly cleaned up!
        Assert.False(File.Exists(stagedA));

        // Verify Server B script is now active!
        var stagedB = Path.Combine(cleoDir, "openmp_server_beta_scriptB.cs");
        Assert.True(File.Exists(stagedB));
        Assert.True(File.Exists(basePersonalScript)); // Personal script still intact!

        // Verify modloader.ini prioritized server_beta and ignored server_alpha!
        var modloaderIni = File.ReadAllText(Path.Combine(modloaderDir, "modloader.ini"));
        Assert.Contains("servers/server_beta=90", modloaderIni);
        Assert.Contains("servers/server_alpha", modloaderIni);
    }

    [Fact]
    public void Test_ModLoaderIni_Idempotent_Activation()
    {
        var modloaderDir = Path.Combine(_tempDir, "modloader");
        Directory.CreateDirectory(modloaderDir);
        var iniContent = """
        [Profiles.Default.Priority]
        ; priorities

        [Profiles.Default.IgnoreMods]
        _ignore
        """;
        var iniPath = Path.Combine(modloaderDir, "modloader.ini");
        File.WriteAllText(iniPath, iniContent);

        var installer = new ModInstaller(_tempDir, "server_gamma");
        // Activate multiple times
        installer.ActivateServerSession("server_gamma");
        installer.ActivateServerSession("server_gamma");
        installer.ActivateServerSession("server_gamma");

        var lines = File.ReadAllLines(iniPath);
        var priorityCount = lines.Count(l => l.Trim().Equals("servers/server_gamma=90", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, priorityCount);
    }

    [Fact]
    public void Test_Server_Rotation_Across_Three_Servers_With_Plugins_And_Text()
    {
        var modloaderDir = Path.Combine(_tempDir, "modloader");
        Directory.CreateDirectory(modloaderDir);
        File.WriteAllText(Path.Combine(modloaderDir, "modloader.ini"), "[Profiles.Default.Priority]\n[Profiles.Default.IgnoreMods]\n_ignore\n");

        var cleoDir = Path.Combine(_tempDir, "cleo");
        Directory.CreateDirectory(cleoDir);

        // Server 1 has plugin1.cleo and script1.js
        var inst1 = new ModInstaller(_tempDir, "sv1");
        var plug1 = Path.Combine(_tempDir, "plug1.cleo");
        File.WriteAllText(plug1, "plug1");
        inst1.InstallFile(new ModManifestItem { Id = "p1", Type = ModType.Script }, new ModFileItem { Path = "cleo/plug1.cleo" }, plug1);
        inst1.ActivateServerSession("sv1");

        Assert.True(File.Exists(Path.Combine(cleoDir, "cleo_plugins", "plug1.cleo")));

        // Server 2 has text2.fxt
        var inst2 = new ModInstaller(_tempDir, "sv2");
        var txt2 = Path.Combine(_tempDir, "text2.fxt");
        File.WriteAllText(txt2, "txt2");
        inst2.InstallFile(new ModManifestItem { Id = "t2", Type = ModType.Script }, new ModFileItem { Path = "cleo/text2.fxt" }, txt2);
        inst2.ActivateServerSession("sv2");

        // Plug 1 removed, text 2 present
        Assert.False(File.Exists(Path.Combine(cleoDir, "cleo_plugins", "plug1.cleo")));
        Assert.True(File.Exists(Path.Combine(cleoDir, "cleo_text", "text2.fxt")));

        // Switch back to Server 1
        inst1.ActivateServerSession("sv1");
        Assert.True(File.Exists(Path.Combine(cleoDir, "cleo_plugins", "plug1.cleo")));
        Assert.False(File.Exists(Path.Combine(cleoDir, "cleo_text", "text2.fxt")));
    }

    [Fact]
    public void Test_Cleo_Audio_Staging_And_Safe_User_File_Restoration()
    {
        var cleoDir = Path.Combine(_tempDir, "cleo");
        Directory.CreateDirectory(cleoDir);

        // User already has a personal openmp_sync.ini in cleo/
        var personalIni = Path.Combine(cleoDir, "openmp_sync.ini");
        File.WriteAllText(personalIni, "[PersonalConfig]\nCustomValue=123\n");

        // Server has siren.wav (audio) and a server-provided openmp_sync.ini (config)
        var inst = new ModInstaller(_tempDir, "server_audio_test");
        var sirenSrc = Path.Combine(_tempDir, "siren.wav");
        File.WriteAllText(sirenSrc, "RIFF_WAV_AUDIO_DATA");
        var iniSrc = Path.Combine(_tempDir, "server_openmp_sync.ini");
        File.WriteAllText(iniSrc, "[ServerConfig]\nServerSync=1\n");

        inst.InstallFile(new ModManifestItem { Id = "audio_mod", Type = ModType.Script }, new ModFileItem { Path = "cleo/audio/siren.wav" }, sirenSrc);
        inst.InstallFile(new ModManifestItem { Id = "config_mod", Type = ModType.Script }, new ModFileItem { Path = "cleo/config/openmp_sync.ini" }, iniSrc);

        // Activate session
        inst.ActivateServerSession("server_audio_test");

        // 1. Verify audio staged to cleo_audio/
        Assert.True(File.Exists(Path.Combine(cleoDir, "cleo_audio", "siren.wav")));

        // 2. Verify server config is staged
        Assert.Equal("[ServerConfig]\nServerSync=1\n", File.ReadAllText(personalIni));

        // 3. Deactivate session
        inst.DeactivateSession();

        // 4. Verify staged audio file is removed
        Assert.False(File.Exists(Path.Combine(cleoDir, "cleo_audio", "siren.wav")));

        // 5. CRITICAL: Verify personal config is RESTORED with original content!
        Assert.True(File.Exists(personalIni));
        Assert.Equal("[PersonalConfig]\nCustomValue=123\n", File.ReadAllText(personalIni));
    }

    [Fact]
    public void Test_DeactivateSession_Restores_Vanilla_State()
    {
        var modloaderDir = Path.Combine(_tempDir, "modloader");
        Directory.CreateDirectory(modloaderDir);
        var iniPath = Path.Combine(modloaderDir, "modloader.ini");
        File.WriteAllText(iniPath, "[Profiles.Default.Priority]\n[Profiles.Default.IgnoreMods]\n_ignore\n");

        var cleoDir = Path.Combine(_tempDir, "cleo");
        Directory.CreateDirectory(cleoDir);

        var inst = new ModInstaller(_tempDir, "server_cleanup_test");
        var scriptSrc = Path.Combine(_tempDir, "test_script.js");
        File.WriteAllText(scriptSrc, "console.log('test');");
        inst.InstallFile(new ModManifestItem { Id = "scr", Type = ModType.Script }, new ModFileItem { Path = "cleo/scripts/test_script.js" }, scriptSrc);

        inst.ActivateServerSession("server_cleanup_test");
        var staged = Path.Combine(cleoDir, "openmp_server_cleanup_test_test_script.js");
        Assert.True(File.Exists(staged));

        var iniContent = File.ReadAllText(iniPath);
        Assert.Contains("servers/server_cleanup_test=90", iniContent);

        // Now cleanly deactivate
        inst.DeactivateSession();

        // Staged script must be deleted
        Assert.False(File.Exists(staged));

        // Priority entry must be removed from modloader.ini
        var iniClean = File.ReadAllText(iniPath);
        Assert.DoesNotContain("servers/server_cleanup_test=90", iniClean);
    }

    [Fact]
    public void Test_PeHeaderModifier_Enables_LargeAddressAware()
    {
        var exePath = Path.Combine(_tempDir, "test_game.exe");
        using (var fs = new FileStream(exePath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // DOS Header (0x40 bytes)
            bw.Write((ushort)0x5A4D); // "MZ"
            bw.Write(new byte[0x3A]); // Padding up to 0x3C
            bw.Write((uint)0x80);     // e_lfanew -> PE offset = 0x80

            // Padding up to 0x80
            bw.Write(new byte[0x80 - 0x40]);

            // PE Header at 0x80
            bw.Write((uint)0x00004550); // "PE\0\0"
            // COFF Header (20 bytes)
            bw.Write((ushort)0x014C);   // Machine i386
            bw.Write((ushort)3);        // NumberOfSections
            bw.Write((uint)0x12345678); // TimeDateStamp
            bw.Write((uint)0);          // PointerToSymbolTable
            bw.Write((uint)0);          // NumberOfSymbols
            bw.Write((ushort)0xE0);     // SizeOfOptionalHeader
            bw.Write((ushort)0x010F);   // Characteristics (32-bit, executable, LAA NOT set)
            // Extra padding
            bw.Write(new byte[256]);
        }

        Assert.False(PeHeaderModifier.IsLargeAddressAware(exePath));

        bool applied = PeHeaderModifier.EnsureLargeAddressAware(exePath, out string status);
        Assert.True(applied);
        Assert.True(PeHeaderModifier.IsLargeAddressAware(exePath));

        // Verify backup was created
        Assert.True(File.Exists(exePath + ".bak"));

        // Idempotent run:
        bool secondRun = PeHeaderModifier.EnsureLargeAddressAware(exePath, out string status2);
        Assert.True(secondRun);
        Assert.Contains("already active", status2);
    }

    [Fact]
    public void Test_ConfigureStreamingMemory_Upgrades_StreamIni_To_2GB()
    {
        var installer = new ModInstaller(_tempDir, "stream_test");
        var streamIni = Path.Combine(_tempDir, "stream.ini");

        // Case 1: Fresh creation
        installer.ConfigureStreamingMemory();
        Assert.True(File.Exists(streamIni));
        var text = File.ReadAllText(streamIni);
        Assert.Contains("memory\t\t2096128", text);
        Assert.Contains("devkit_memory\t2096128", text);
        Assert.Contains("vehicles\t96", text);

        // Case 2: Upgrade existing legacy stream.ini
        File.WriteAllText(streamIni, "memory 13500\r\ndevkit_memory 13500\r\nvehicles 36\r\n");
        installer.ConfigureStreamingMemory();
        var updatedText = File.ReadAllText(streamIni);
        Assert.Contains("memory\t\t2096128", updatedText);
        Assert.Contains("devkit_memory\t2096128", updatedText);
        Assert.Contains("vehicles\t96", updatedText);
        Assert.DoesNotContain("13500", updatedText);
    }

    [Fact]
    public void Test_GameInstallation_GtaSaExe_IsLargeAddressAware()
    {
        var gameExePath = @"c:\Users\Benja\Desktop\OPENMP MODS SYNC\GTA San Andreas Instalador victor17798\gta_sa.exe";
        if (File.Exists(gameExePath))
        {
            Assert.True(PeHeaderModifier.IsLargeAddressAware(gameExePath));
        }
    }

    [Fact]
    public void Test_ModSyncSdk_Targets_StreamingBudgetAddress_Not_MemoryUsed()
    {
        var sdkPath = @"c:\Users\Benja\Desktop\OPENMP MODS SYNC\GTA San Andreas Instalador victor17798\cleo\modsync_sdk.js";
        if (File.Exists(sdkPath))
        {
            var content = File.ReadAllText(sdkPath);
            // Must write budget to 0x8A5A80 (ms_memoryAvailable)
            Assert.Contains("0x8A5A80", content);
            // Must NOT write 2047MB to 0x8E4CB4 (which is ms_memoryUsed)
            Assert.DoesNotContain("Memory.WriteU32(0x8E4CB4, 2047", content);
        }
    }

    [Fact]
    public void Test_GtaSaExe_CheckForDuplicateProcess_And_Graphics_Bytecode()
    {
        var gameExePath = @"c:\Users\Benja\Desktop\OPENMP MODS SYNC\GTA San Andreas Instalador victor17798\gta_sa.exe";
        if (File.Exists(gameExePath))
        {
            var bytes = File.ReadAllBytes(gameExePath);
            Assert.True(bytes.Length > 0x003468E5);

            // 0x00345CE0 (VA 0x007468E0): CheckForDuplicateProcess must be patched with 31 C0 C3 90 90
            byte[] expectedDupPatch = [0x31, 0xC0, 0xC3, 0x90, 0x90];
            Assert.Equal(expectedDupPatch, bytes.AsSpan(0x00345CE0, 5).ToArray());

            // 0x003468E0 (VA 0x007474E0): InitialiseGraphicsLib must retain original opcode 35 68 CF C8 00
            byte[] expectedGfxBytes = [0x35, 0x68, 0xCF, 0xC8, 0x00];
            Assert.Equal(expectedGfxBytes, bytes.AsSpan(0x003468E0, 5).ToArray());
        }
    }

    [Fact]
    public void Test_ModLoader_Bytecode_And_Config()
    {
        var mlPath = @"c:\Users\Benja\Desktop\OPENMP MODS SYNC\GTA San Andreas Instalador victor17798\modloader.asi";
        if (File.Exists(mlPath))
        {
            var bytes = File.ReadAllBytes(mlPath);
            Assert.True(bytes.Length > 0x9D05);
            // 0x9D00 must be original opcode 0x6A (push 0x10), not 0xC3 (ret)
            Assert.Equal(0x6A, bytes[0x9D00]);
        }

        var configPath = @"c:\Users\Benja\Desktop\OPENMP MODS SYNC\GTA San Andreas Instalador victor17798\modloader\.data\config.ini";
        if (File.Exists(configPath))
        {
            var text = File.ReadAllText(configPath);
            Assert.Contains("EnableMenu", text);
            Assert.DoesNotContain("EnableMenu = true", text);
            Assert.DoesNotContain("EnableMenu        = true", text);
        }
    }
}
