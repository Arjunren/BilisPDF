[CmdletBinding()]
param(
    [string] $ExternalLocation = (Join-Path (Split-Path -Parent $PSScriptRoot) 'payload'),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$manifest = Join-Path $repositoryRoot "artifacts\modern-shell\$Configuration\layout\AppxManifest.xml"
if (-not (Test-Path -LiteralPath $manifest)) { throw "Build the installer payload first. Missing $manifest" }
if (-not (Test-Path -LiteralPath (Join-Path $ExternalLocation 'EasyPDF.ShellExtension.dll'))) {
    throw 'The external payload does not contain EasyPDF.ShellExtension.dll.'
}

Get-AppxPackage -Name 'Arjunren.EasyPDF.Shell' -ErrorAction SilentlyContinue | Remove-AppxPackage
Add-AppxPackage -Register $manifest -ExternalLocation (Resolve-Path -LiteralPath $ExternalLocation).Path
