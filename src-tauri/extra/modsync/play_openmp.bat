@echo off
title open.mp 0.4.0 - R1 ModSync Client (eLdarqO)
taskkill /F /IM gta_sa.exe 2>nul
taskkill /F /IM sampcmd.exe 2>nul
ModSyncLauncher.exe --auto-launch
if %ERRORLEVEL% neq 0 pause
