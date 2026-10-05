#[cfg(target_os = "windows")]
use log::info;
#[cfg(target_os = "windows")]
use std::path::PathBuf;
#[cfg(target_os = "windows")]
use std::process::{Command, Stdio};

#[cfg(target_os = "windows")]
use crate::{constants::*, errors::*};

#[cfg(not(target_os = "windows"))]
pub async fn run_samp(
    _name: &str,
    _ip: &str,
    _port: i32,
    _executable_dir: &str,
    _dll_path: &str,
    _omp_file: &str,
    _password: &str,
    _custom_game_exe: &str,
) -> Result<()> {
    Ok(())
}

#[cfg(target_os = "windows")]
pub async fn run_samp(
    name: &str,
    ip: &str,
    port: i32,
    executable_dir: &str,
    dll_path: &str,
    omp_file: &str,
    password: &str,
    custom_game_exe: &str,
) -> Result<()> {
    // Prepare the command to spawn the executable
    let target_game_exe = if custom_game_exe.len() > 0 {
        custom_game_exe.to_string()
    } else {
        GTA_SA_EXECUTABLE.to_string()
    };

    let exe_path = PathBuf::from(executable_dir).join(&target_game_exe);

    // Terminate any lingering zombie gta_sa or sampcmd processes silently before launching
    crate::modsync::terminate_lingering_game_processes();

    // Automatically optimize GTA SA for ModSync: 2048 MB memory budget, door animations matrix pool, and LAA flag
    let _ = crate::modsync::ensure_modsync_framework(executable_dir);

    let exe_path = exe_path.canonicalize().map_err(|e| {
        LauncherError::Process(format!("Invalid executable path {:?}: {}", exe_path, e))
    })?;

    let mut cmd = Command::new(&exe_path);

    let mut ready_for_exec = cmd
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .current_dir(executable_dir)
        .arg("-c")
        .arg("-n")
        .arg(name)
        .arg("-h")
        .arg(ip)
        .arg("-p")
        .arg(format!("{}", port));

    if !password.is_empty() {
        ready_for_exec = ready_for_exec.arg("-z").arg(password);
    }

    let process = ready_for_exec.current_dir(executable_dir).spawn();

    match process {
        Ok(p) => {
            inject_dll(p.id(), dll_path, 0, false)?;
            info!("[run_samp] omp_file.is_empty(): {}", omp_file.is_empty());
            if !omp_file.is_empty() {
                inject_dll(p.id(), omp_file, 0, false)
            } else {
                Ok(())
            }
        }
        Err(e) => {
            info!("[injector.rs] Process creation failed: {}", e);

            match e.raw_os_error() {
                Some(ERROR_ELEVATION_REQUIRED) => Err(LauncherError::AccessDenied(
                    "Unable to open game process".to_string(),
                )),
                Some(ERROR_ACCESS_DENIED) => Err(LauncherError::AccessDenied(
                    "Unable to open game process".to_string(),
                )),
                _ => Err(LauncherError::Process(format!(
                    "Failed to spawn process: {}",
                    e
                ))),
            }
        }
    }
}

#[cfg(target_os = "windows")]
pub fn inject_dll(child: u32, dll_path: &str, times: u32, waiting_for_vorbis: bool) -> Result<()> {
    use std::ffi::CString;
    use winapi::{
        shared::minwindef::{FALSE, HMODULE},
        um::{
            handleapi::CloseHandle,
            libloaderapi::{GetModuleHandleA, GetProcAddress},
            memoryapi::{VirtualAllocEx, VirtualFreeEx, WriteProcessMemory},
            processthreadsapi::{CreateRemoteThread, OpenProcess},
            psapi::{EnumProcessModulesEx, GetModuleFileNameExA},
            synchapi::WaitForSingleObject,
            winnt::{
                MEM_COMMIT, MEM_RELEASE, MEM_RESERVE, PAGE_READWRITE, PROCESS_CREATE_THREAD,
                PROCESS_QUERY_INFORMATION, PROCESS_VM_OPERATION, PROCESS_VM_READ, PROCESS_VM_WRITE,
            },
        },
    };

    let desired_access = PROCESS_CREATE_THREAD
        | PROCESS_QUERY_INFORMATION
        | PROCESS_VM_OPERATION
        | PROCESS_VM_WRITE
        | PROCESS_VM_READ;

    unsafe {
        let handle = OpenProcess(desired_access, FALSE, child);
        if handle.is_null() {
            let err = std::io::Error::last_os_error();
            match err.raw_os_error() {
                Some(ERROR_ELEVATION_REQUIRED) | Some(ERROR_ACCESS_DENIED) => {
                    return Err(LauncherError::AccessDenied("Unable to open game process".to_string()));
                }
                _ => {
                    return Err(LauncherError::Process(format!("Failed to access process: {}", err)));
                }
            }
        }

        if waiting_for_vorbis {
            let mut module_handles: [HMODULE; PROCESS_MODULE_BUFFER_SIZE] =
                [0 as *mut _; PROCESS_MODULE_BUFFER_SIZE];
            let mut found = 0;

            EnumProcessModulesEx(
                handle,
                module_handles.as_mut_ptr(),
                module_handles.len() as _,
                &mut found,
                0x03,
            );

            let mut bytes = [0i8; PROCESS_MODULE_BUFFER_SIZE];

            if found == 0 {
                CloseHandle(handle);
                let delay = std::time::Duration::from_millis(INJECTION_RETRY_DELAY_MS);
                std::thread::sleep(delay);
                return inject_dll(child, dll_path, times, true);
            }

            let mut found_vorbis = false;
            for i in 0..(found / 4) {
                if GetModuleFileNameExA(
                    handle,
                    module_handles[i as usize],
                    bytes.as_mut_ptr(),
                    PROCESS_MODULE_BUFFER_SIZE as u32,
                ) != 0
                {
                    let string = std::ffi::CStr::from_ptr(bytes.as_ptr());
                    if string.to_string_lossy().to_string().contains("vorbis") {
                        found_vorbis = true;
                        break;
                    }
                }
            }

            if !found_vorbis {
                CloseHandle(handle);
                let delay = std::time::Duration::from_millis(INJECTION_RETRY_DELAY_MS);
                std::thread::sleep(delay);
                return inject_dll(child, dll_path, times, true);
            }
        }

        let c_path = match CString::new(dll_path) {
            Ok(p) => p,
            Err(e) => {
                CloseHandle(handle);
                return Err(LauncherError::Injection(e.to_string()));
            }
        };
        let path_bytes = c_path.as_bytes_with_nul();

        let kernel32 = GetModuleHandleA(b"kernel32.dll\0".as_ptr() as *const i8);
        if kernel32.is_null() {
            CloseHandle(handle);
            return Err(LauncherError::Injection("GetModuleHandleA kernel32 failed".to_string()));
        }

        let load_library = GetProcAddress(kernel32, b"LoadLibraryA\0".as_ptr() as *const i8);
        if load_library.is_null() {
            CloseHandle(handle);
            return Err(LauncherError::Injection("GetProcAddress LoadLibraryA failed".to_string()));
        }

        let remote_mem = VirtualAllocEx(
            handle,
            std::ptr::null_mut(),
            path_bytes.len(),
            MEM_COMMIT | MEM_RESERVE,
            PAGE_READWRITE,
        );
        if remote_mem.is_null() {
            CloseHandle(handle);
            return Err(LauncherError::Injection("VirtualAllocEx failed".to_string()));
        }

        let mut bytes_written = 0;
        let write_ok = WriteProcessMemory(
            handle,
            remote_mem,
            path_bytes.as_ptr() as *const _,
            path_bytes.len(),
            &mut bytes_written,
        );
        if write_ok == 0 {
            VirtualFreeEx(handle, remote_mem, 0, MEM_RELEASE);
            CloseHandle(handle);
            return Err(LauncherError::Injection("WriteProcessMemory failed".to_string()));
        }

        let thread = CreateRemoteThread(
            handle,
            std::ptr::null_mut(),
            0,
            Some(std::mem::transmute(load_library)),
            remote_mem,
            0,
            std::ptr::null_mut(),
        );

        if thread.is_null() {
            VirtualFreeEx(handle, remote_mem, 0, MEM_RELEASE);
            CloseHandle(handle);
            return Err(LauncherError::Injection("CreateRemoteThread failed".to_string()));
        }

        WaitForSingleObject(thread, 5000);
        CloseHandle(thread);
        VirtualFreeEx(handle, remote_mem, 0, MEM_RELEASE);
        CloseHandle(handle);
    }

    Ok(())
}
