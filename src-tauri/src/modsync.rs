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

pub fn ensure_modsync_framework<P: AsRef<Path>>(gtasa_path: P) -> Result<(), String> {
    let base_dir = gtasa_path.as_ref();
    if !base_dir.exists() {
        return Err(format!("GTA San Andreas directory does not exist: {:?}", base_dir));
    }

    // 1. Ensure Large Address Aware flag on gta_sa.exe
    let exe_path = base_dir.join("gta_sa.exe");
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

    // 2. Ensure stream.ini exists with 2048 MB budget
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

    // 3. Ensure modsync_hook.asi is deployed
    let hook_dest = base_dir.join("modsync_hook.asi");
    if !hook_dest.exists() {
        let possible_sources = [
            PathBuf::from("extra/modsync/modsync_hook.asi"),
            PathBuf::from("src-tauri/extra/modsync/modsync_hook.asi"),
            PathBuf::from("../extra/modsync/modsync_hook.asi"),
        ];

        for src in &possible_sources {
            if src.exists() {
                if let Err(e) = fs::copy(src, &hook_dest) {
                    warn!("Failed to copy modsync_hook.asi from {:?}: {}", src, e);
                } else {
                    info!("Deployed modsync_hook.asi to {:?}", hook_dest);
                    break;
                }
            }
        }
    }

    Ok(())
}
