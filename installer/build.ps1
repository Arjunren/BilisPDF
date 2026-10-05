[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$payloadDirectory = Join-Path $PSScriptRoot 'payload'

if (Test-Path -LiteralPath $payloadDirectory) {
    Remove-Item -LiteralPath $payloadDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $payloadDirectory | Out-Null

$projects = @(
    'src/EasyPDF.Desktop/EasyPDF.Desktop.csproj',
    'src/EasyPDF.Cli/EasyPDF.Cli.csproj',
    'src/EasyPDF.Shell/EasyPDF.Shell.csproj'
)

foreach ($project in $projects) {
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    dotnet publish (Join-Path $repositoryRoot $project) -c $Configuration -r win-x64 --self-contained $selfContained -p:PublishSingleFile=false -o $payloadDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}

$shellPackage = Join-Path $PSScriptRoot "bin\x64\$Configuration\EasyPDF.ShellPackage.msix"
& (Join-Path $PSScriptRoot 'modern-shell\build.ps1') -Configuration $Configuration -ExternalPayload $payloadDirectory -PackageOutput $shellPackage
if ($LASTEXITCODE -ne 0) { throw 'Building the Windows 11 Explorer package failed.' }

$registerModernShell = 'false'
if ($env:EASYPDF_SIGNING_CERT_PATH) {
    Copy-Item -LiteralPath $shellPackage -Destination $payloadDirectory
    $registerModernShell = 'true'
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $payloadDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'TERMS.md') -Destination $payloadDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $payloadDirectory

dotnet build (Join-Path $PSScriptRoot 'EasyPDF.Installer.wixproj') -c $Configuration -p:RegisterModernShell=$registerModernShell
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed.' }
