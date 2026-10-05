use std::fs::{self, OpenOptions};
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::{Path, PathBuf};
use log::{info, warn};

const LAA_FLAG: u16 = 0x0020;
const STREAM_INI_CONTENT: &str = r#"; ModSync Stream Memory Configuration
; Streaming memory budget (in kilobytes): 2048 MB = 2097152 KB
memory 2097152
"#;

pub fn patch_large_address_aware<P: AsRef<Path>>(exe_path: P) -> Result<bool, String> {
    let path = exe_path.as_ref();
    if !path.exists() {
        return Err(format!("File does not exist: {:?}", path));
    }

    let mut file = OpenOptions::new()
        .read(true)
        .write(true)
        .open(path)
        .map_err(|e| format!("Failed to open executable for LAA patching: {}", e))?;

    // Verify DOS MZ header
    let mut dos_header = [0u8; 64];
    file.read_exact(&mut dos_header)
        .map_err(|e| format!("Failed to read DOS header: {}", e))?;

    if dos_header[0] != 0x4D || dos_header[1] != 0x5A {
        return Err("Not a valid PE executable (missing MZ signature)".to_string());
    }

    // Read PE offset at 0x3C
    let pe_offset = u32::from_le_bytes([
        dos_header[0x3C],
        dos_header[0x3D],
        dos_header[0x3E],
        dos_header[0x3F],
    ]) as u64;

    file.seek(SeekFrom::Start(pe_offset))
        .map_err(|e| format!("Failed to seek to PE header: {}", e))?;

    // Verify PE signature ("PE\0\0")
    let mut pe_sig = [0u8; 4];
    file.read_exact(&mut pe_sig)
        .map_err(|e| format!("Failed to read PE signature: {}", e))?;

    if pe_sig != [0x50, 0x45, 0x00, 0x00] {
        return Err("Not a valid PE header (missing PE signature)".to_string());
    }

    // Characteristics offset is pe_offset + 4 (signature) + 18 (COFF header offset to characteristics) = pe_offset + 22
    let characteristics_offset = pe_offset + 22;
    file.seek(SeekFrom::Start(characteristics_offset))
        .map_err(|e| format!("Failed to seek to PE characteristics: {}", e))?;

    let mut char_bytes = [0u8; 2];
    file.read_exact(&mut char_bytes)
        .map_err(|e| format!("Failed to read characteristics: {}", e))?;

    let characteristics = u16::from_le_bytes(char_bytes);

    if (characteristics & LAA_FLAG) == LAA_FLAG {
        info!("Executable is already Large Address Aware: {:?}", path);
        return Ok(false);
    }

    let new_characteristics = characteristics | LAA_FLAG;
    file.seek(SeekFrom::Start(characteristics_offset))
        .map_err(|e| format!("Failed to seek to characteristics for writing: {}", e))?;

    file.write_all(&new_characteristics.to_le_bytes())
        .map_err(|e| format!("Failed to write updated characteristics: {}", e))?;

    info!(
        "Successfully patched executable with Large Address Aware (0x0020): {:?}",
        path
    );
    Ok(true)
}

pub fn find_modsync_source_dir() -> Option<PathBuf> {
    if let Ok(exe_path) = std::env::current_exe() {
        if let Some(exe_dir) = exe_path.parent() {
            let candidate = exe_dir.join("extra").join("modsync");
            if candidate.exists() {
                return Some(candidate);
            }
        }
    }

    let candidates = [
        PathBuf::from("extra/modsync"),
        PathBuf::from("src-tauri/extra/modsync"),
        PathBuf::from("../extra/modsync"),
        PathBuf::from(r"C:\Users\Benja\Desktop\OPENMP MODS SYNC\build\launcher\mod_framework"),
    ];

    for c in &candidates {
        if c.exists() {
            return Some(c.clone());
        }
    }

    None
}

fn copy_dir_recursive(src: &Path, dst: &Path) -> std::io::Result<()> {
    if !dst.exists() {
        fs::create_dir_all(dst)?;
    }

    for entry in fs::read_dir(src)? {
        let entry = entry?;
        let file_type = entry.file_type()?;
        let target = dst.join(entry.file_name());

        if file_type.is_dir() {
            copy_dir_recursive(&entry.path(), &target)?;
        } else {
            let should_copy = match (fs::metadata(entry.path()), fs::metadata(&target)) {
                (Ok(s), Ok(d)) => s.len() != d.len(),
                (Ok(_), Err(_)) => true,
                _ => false,
            };
            if should_copy {
                let _ = fs::copy(entry.path(), target);
            }
        }
    }
    Ok(())
}

fn copy_file_if_needed(src: &Path, dst: &Path) {
    if !src.exists() {
        return;
    }
    let should_copy = match (fs::metadata(src), fs::metadata(dst)) {
        (Ok(s), Ok(d)) => s.len() != d.len(),
        (Ok(_), Err(_)) => true,
        _ => false,
    };
    if should_copy {
        if let Some(parent) = dst.parent() {
            let _ = fs::create_dir_all(parent);
        }
        if let Err(e) = fs::copy(src, dst) {
            warn!("Failed to copy {:?} to {:?}: {}", src, dst, e);
        } else {
            info!("Deployed {:?} -> {:?}", src, dst);
        }
    }
}

pub fn ensure_modsync_framework<P: AsRef<Path>>(gtasa_path: P) -> Result<(), String> {
    let base_dir = gtasa_path.as_ref();
    if !base_dir.exists() {
        return Err(format!("GTA San Andreas directory does not exist: {:?}", base_dir));
    }

    let src_dir_opt = find_modsync_source_dir();

    // 1. Ensure compatible GTA San Andreas 1.0 US executable (14,383,616 bytes)
    let exe_path = base_dir.join("gta_sa.exe");
    let needs_exe_deployment = if exe_path.exists() {
        match fs::metadata(&exe_path) {
            Ok(meta) => meta.len() != 14_383_616,
            Err(_) => true,
        }
    } else {
        true
    };

    if needs_exe_deployment {
        if let Some(ref src_dir) = src_dir_opt {
            let bundled_exe = src_dir.join("gta_sa.exe");
            if bundled_exe.exists() {
                if exe_path.exists() {
                    let backup_path = base_dir.join("gta_sa.exe.unsupported.bak");
                    if !backup_path.exists() {
                        let _ = fs::rename(&exe_path, &backup_path);
                    } else {
                        let _ = fs::remove_file(&exe_path);
                    }
                }
                if let Err(e) = fs::copy(&bundled_exe, &exe_path) {
                    warn!("Failed to deploy GTA SA 1.0 US executable: {}", e);
                } else {
                    info!("Successfully deployed compatible GTA SA 1.0 US executable to {:?}", exe_path);
                }
            }
        }
    }

    // 2. Ensure Large Address Aware flag on gta_sa.exe
    if exe_path.exists() {
        match patch_large_address_aware(&exe_path) {
            Ok(modified) => {
                if modified {
                    info!("LAA flag applied to {:?}", exe_path);
                }
            }
            Err(e) => {
                warn!("Could not apply LAA patch to {:?}: {}", exe_path, e);
            }
        }
    }

    // 3. Ensure stream.ini exists with 2048 MB budget
    let stream_ini = base_dir.join("stream.ini");
    let needs_stream_write = match fs::read_to_string(&stream_ini) {
        Ok(content) => !content.contains("2097152"),
        Err(_) => true,
    };

    if needs_stream_write {
        if let Err(e) = fs::write(&stream_ini, STREAM_INI_CONTENT) {
            warn!("Could not write stream.ini to {:?}: {}", stream_ini, e);
        } else {
            info!("Configured stream.ini (2048 MB memory budget) in {:?}", stream_ini);
        }
    }

    // 4. Deploy full ModSync framework if source directory is found
    if let Some(src_dir) = src_dir_opt {
        let core_files = [
            "gta_sa.exe",
            "vorbisFile.dll",
            "vorbisHooked.dll",
            "vorbis.dll",
            "ogg.dll",
            "dinput8.dll",
            "CLEO.asi",
            "cleo_redux.asi",
            ".cleo_config.ini",
            "modloader.asi",
            "modloader.ini",
            "SilentPatchSA.asi",
            "SilentPatchSA.ini",
            "d3dx9_25.dll",
            "sampcmd.exe",
            "modsync_hook.asi",
            "ModSyncLauncher.exe",
        ];

        for filename in &core_files {
            let src = src_dir.join(filename);
            let dst = base_dir.join(filename);
            copy_file_if_needed(&src, &dst);
        }

        // Directories: cleo and modloader/.data
        let cleo_src = src_dir.join("cleo");
        let cleo_dst = base_dir.join("cleo");
        if cleo_src.exists() {
            let _ = copy_dir_recursive(&cleo_src, &cleo_dst);
        }

        let ml_data_src = src_dir.join("modloader").join(".data");
        let ml_data_dst = base_dir.join("modloader").join(".data");
        if ml_data_src.exists() {
            let _ = copy_dir_recursive(&ml_data_src, &ml_data_dst);
        }

        let _ = fs::create_dir_all(base_dir.join("modloader").join("servers"));
        let _ = fs::create_dir_all(base_dir.join("cleo").join("servers"));

        // Deploy 0.4.0 - R1 client DLL to %LOCALAPPDATA%\mp.open.launcher\samp\0.4.0-R1\samp.dll
        if let Ok(local_app_data) = std::env::var("LOCALAPPDATA") {
            let target_040_dir = PathBuf::from(local_app_data)
                .join("mp.open.launcher")
                .join("samp")
                .join("0.4.0-R1");
            let target_040_dll = target_040_dir.join("samp.dll");

            let possible_040_src = [
                src_dir.join("040R1_samp.dll"),
                src_dir.join("samp.dll"),
            ];

            for src in &possible_040_src {
                if src.exists() {
                    let _ = fs::create_dir_all(&target_040_dir);
                    copy_file_if_needed(src, &target_040_dll);
                    break;
                }
            }
        }
    }

    Ok(())
}

pub fn sync_modsync_session<P: AsRef<Path>>(
    gtasa_path: P,
    server_ip: &str,
    server_port: i32,
    player_name: &str,
    server_id: &str,
    cdn_url: Option<&str>,
) -> Result<(), String> {
    let base_dir = gtasa_path.as_ref();
    let local_launcher = base_dir.join("ModSyncLauncher.exe");

    let launcher_exe = if local_launcher.exists() {
        local_launcher
    } else if let Some(src_dir) = find_modsync_source_dir() {
        src_dir.join("ModSyncLauncher.exe")
    } else {
        return Err("ModSyncLauncher.exe not found".to_string());
    };

    if !launcher_exe.exists() {
        return Err(format!("ModSyncLauncher executable not found at {:?}", launcher_exe));
    }

    let mut cmd = std::process::Command::new(&launcher_exe);
    cmd.arg("--gta-path")
        .arg(base_dir)
        .arg("--server-id")
        .arg(server_id)
        .arg("--host")
        .arg(server_ip)
        .arg("--port")
        .arg(server_port.to_string())
        .arg("--player-name")
        .arg(player_name)
        .arg("--sync-only");

    if let Some(cdn) = cdn_url {
        if !cdn.is_empty() {
            cmd.arg("--cdn-url").arg(cdn);
        }
    }

    cmd.current_dir(base_dir);

    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        cmd.creation_flags(0x08000000); // CREATE_NO_WINDOW
    }

    match cmd.output() {
        Ok(output) => {
            if output.status.success() {
                info!("ModSync pre-sync completed successfully for {}", server_id);
                Ok(())
            } else {
                let err = String::from_utf8_lossy(&output.stderr);
                warn!("ModSync pre-sync exited with warning: {}", err);
                Ok(())
            }
        }
        Err(e) => {
            warn!("Failed to execute ModSync pre-sync: {}", e);
            Ok(())
        }
    }
}
