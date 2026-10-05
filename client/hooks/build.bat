@echo off
setlocal
echo ===============================================================================
echo ModSync Native ASI Hook Build Script
echo Target: GTA San Andreas (32-bit x86)
echo ===============================================================================

if not exist build mkdir build
cd build

cmake .. -A Win32 -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 (
    echo [ERROR] CMake configuration failed.
    exit /b 1
)

cmake --build . --config Release
if errorlevel 1 (
    echo [ERROR] Build compilation failed.
    exit /b 1
)

if exist Release\modsync_hook.asi (
    copy /Y Release\modsync_hook.asi ..\modsync_hook.asi
    echo [SUCCESS] modsync_hook.asi built and updated successfully.
) else (
    echo [ERROR] Target binary Release\modsync_hook.asi not found.
    exit /b 1
)

endlocal
