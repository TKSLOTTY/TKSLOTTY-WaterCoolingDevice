@echo off
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:UsbScreenTest.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll Program.cs
if errorlevel 1 (
 pause
 exit /b 1
)
start "" "%~dp0UsbScreenTest.exe"

