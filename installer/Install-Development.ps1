[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'payload'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\EasyPDF'
$developerMode = Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue
if ($developerMode -ne 1) { throw 'Development registration requires Windows Developer Mode. Use the signed release installer on other machines.' }
foreach ($name in @('EasyPDF.Desktop.exe', 'EasyPDF.Shell.exe', 'EasyPDF.ShellExtension.dll', 'AppxManifest.xml', 'Register-DevelopmentPackage.ps1', 'EasyPDF.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Build the payload with installer/build.ps1 first. Missing $name" }
}
$running = Get-Process -Name 'EasyPDF.Desktop', 'EasyPDF.Shell' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw 'Close EasyPDF before updating its development installation.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& robocopy $source $destination /E /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not update the payload. Close EasyPDF and File Explorer windows, then try again.' }
& (Join-Path $destination 'Register-DevelopmentPackage.ps1') -ExternalLocation $destination

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'EasyPDF'))) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'EasyPDF.lnk'))
    $shortcut.TargetPath = Join-Path $destination 'EasyPDF.Desktop.exe'
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = Join-Path $destination 'EasyPDF.ico'
    $shortcut.Save()
}
Write-Host "Updated the current user's EasyPDF app and shortcuts: $destination"
$global:LASTEXITCODE = 0
