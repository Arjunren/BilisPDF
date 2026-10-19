[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'payload'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\BilisPDF'
$developerMode = Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue
if ($developerMode -ne 1) { throw 'Development registration requires Windows Developer Mode. Use the signed release installer on other machines.' }
foreach ($name in @('BilisPDF.Desktop.exe', 'BilisPDF.Shell.exe', 'BilisPDF.ShellExtension.dll', 'AppxManifest.xml', 'Register-DevelopmentPackage.ps1', 'BilisPDF.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Build the payload with installer/build.ps1 first. Missing $name" }
}
$running = Get-Process -Name 'BilisPDF.Desktop', 'BilisPDF.Shell' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw 'Close BilisPDF before updating its development installation.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& robocopy $source $destination /E /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not update the payload. Close BilisPDF and File Explorer windows, then try again.' }
& (Join-Path $destination 'Register-DevelopmentPackage.ps1') -ExternalLocation $destination

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'BilisPDF'))) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'BilisPDF.lnk'))
    $shortcut.TargetPath = Join-Path $destination 'BilisPDF.Desktop.exe'
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = Join-Path $destination 'BilisPDF.ico'
    $shortcut.Save()
}
$legacyDirectory = Join-Path $env:LOCALAPPDATA 'Programs\EasyPDF'
foreach ($legacyShortcut in @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'EasyPDF.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'EasyPDF\EasyPDF.lnk')
)) {
    if ((Test-Path -LiteralPath $legacyShortcut) -and
        $shell.CreateShortcut($legacyShortcut).TargetPath -eq (Join-Path $legacyDirectory 'EasyPDF.Desktop.exe')) {
        Remove-Item -LiteralPath $legacyShortcut
    }
}
Write-Host "Updated the current user's BilisPDF app and shortcuts: $destination"
$global:LASTEXITCODE = 0
