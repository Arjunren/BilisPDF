[CmdletBinding()]
param(
    [string] $ExternalLocation
)

$ErrorActionPreference = 'Stop'
if (-not $ExternalLocation) {
    $ExternalLocation = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'EasyPDF.ShellExtension.dll')) { $PSScriptRoot } else { Join-Path (Split-Path -Parent $PSScriptRoot) 'payload' }
}
$ExternalLocation = (Resolve-Path -LiteralPath $ExternalLocation).Path
$manifest = Join-Path $ExternalLocation 'AppxManifest.xml'
foreach ($required in @('AppxManifest.xml', 'EasyPDF.ShellExtension.dll', 'EasyPDF.Shell.exe', 'EasyPDF.Desktop.exe', 'EasyPDF.ico', 'Assets\StoreLogo.png', 'Assets\Square44x44Logo.png', 'Assets\Square150x150Logo.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ExternalLocation $required) -PathType Leaf)) { throw "Incomplete installation: missing $required" }
}

# Keep the manifest beside the installed binaries; build folders are disposable.
Add-AppxPackage -Register $manifest -ExternalLocation $ExternalLocation -ForceApplicationShutdown
$package = Get-AppxPackage -Name 'Arjunren.EasyPDF.Shell'
if (-not $package -or $package.Status -ne 'Ok') { throw 'Windows did not register the EasyPDF Explorer package.' }
$registeredManifest = $package | Get-AppxPackageManifest
if ($registeredManifest.Package.Identity.Version -ne ([xml](Get-Content -LiteralPath $manifest -Raw)).Package.Identity.Version) {
    throw 'Windows still has a different version of the Explorer package registered.'
}
Write-Host 'EasyPDF Explorer menu registered. Close and reopen File Explorer windows if the old menu is cached.'
