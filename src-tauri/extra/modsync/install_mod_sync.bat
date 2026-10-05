@echo off
title open.mp ModSync 0.4.0 - R1 - Client Setup (eLdarqO)
echo ==================================================
echo   open.mp ModSync 0.4.0 - R1 Automated Setup
echo   Architect: eLdarqO
echo ==================================================
echo.
set TARGET_DIR=%~dp0
if not exist "%TARGET_DIR%gta_sa.exe" (
    set /p USER_PATH="Enter path to your GTA San Andreas directory (or press Enter if installed here): "
    if not "%USER_PATH%"=="" set TARGET_DIR=%USER_PATH%\
)
echo Installing ModSync framework to: %TARGET_DIR%

if not exist "%TARGET_DIR%gta_sa.exe" (
    echo [ERROR] gta_sa.exe not found in specified directory.
    pause
    exit /b 1
)

echo [1/5] Deploying ASI Loader and Client Libraries...
xcopy /Y /Q "mod_framework\*.*" "%TARGET_DIR%"

echo [2/5] Deploying ModLoader Framework and Modpack...
if not exist "%TARGET_DIR%modloader" mkdir "%TARGET_DIR%modloader"
xcopy /E /Y /Q "mod_framework\modloader\*" "%TARGET_DIR%modloader\"

echo [3/5] Deploying CLEO Redux Engine and Compatibility Fixes...
if not exist "%TARGET_DIR%cleo" mkdir "%TARGET_DIR%cleo"
xcopy /E /Y /Q "mod_framework\cleo\*" "%TARGET_DIR%cleo\"

echo [4/5] Installing ModSync Launcher and Config...
copy /Y "ModSyncLauncher.exe" "%TARGET_DIR%ModSyncLauncher.exe"
copy /Y "play_openmp.bat" "%TARGET_DIR%play_openmp.bat"
if exist "launcher_config.json" copy /Y "launcher_config.json" "%TARGET_DIR%launcher_config.json"

echo [5/5] Pre-synchronizing Game Archives (gta3.img)...
pushd "%TARGET_DIR%"
"%TARGET_DIR%ModSyncLauncher.exe" --sync-only
popd

echo ==================================================
echo   Installation Complete! Ready to play.
echo   Run 'play_openmp.bat' to launch and synchronize mods.
echo ==================================================
pause
