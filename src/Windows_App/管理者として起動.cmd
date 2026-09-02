@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~dp0WaterCoolingDevice.exe' -WorkingDirectory '%~dp0' -Verb RunAs"
