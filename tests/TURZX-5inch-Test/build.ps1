$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /out:"$PSScriptRoot\UsbScreenTest.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll "$PSScriptRoot\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Built UsbScreenTest.exe'
