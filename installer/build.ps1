[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $FrameworkDependent,
    [switch] $Release
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
$signRelease = $Release -or [bool]$env:BILISPDF_SIGNING_CERT_THUMBPRINT
$publisher = 'CN=Arjunren Von'
if ($signRelease) {
    if (-not $env:BILISPDF_SIGNING_CERT_THUMBPRINT -or -not $env:BILISPDF_TIMESTAMP_URL) {
        throw 'A signed release requires BILISPDF_SIGNING_CERT_THUMBPRINT and BILISPDF_TIMESTAMP_URL. No trusted signing identity is configured.'
    }
    if ($env:BILISPDF_SIGNING_CERT_THUMBPRINT -notmatch '^[A-Fa-f0-9]{40}$') { throw 'The signing certificate thumbprint must be 40 hexadecimal characters.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$env:BILISPDF_SIGNING_CERT_THUMBPRINT"
    $enhancedKeyUsage = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }
    if (-not $certificate.HasPrivateKey -or $certificate.Subject -eq $certificate.Issuer -or
        $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date) -or -not $certificate.Verify() -or
        '1.3.6.1.5.5.7.3.3' -notin $enhancedKeyUsage.EnhancedKeyUsages.Value) {
        throw 'A valid, trusted code-signing certificate with an accessible private key is required. Self-signed development certificates are not accepted for release.'
    }
    $publisher = $certificate.Subject
}
if ($env:BILISPDF_SIGNING_CERT_PATH) { throw 'Import your certificate securely into CurrentUser\My, then set BILISPDF_SIGNING_CERT_THUMBPRINT. PFX passwords are not passed to SignTool.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$payloadDirectory = Join-Path $PSScriptRoot 'payload'

if (Test-Path -LiteralPath $payloadDirectory) {
    if ((Resolve-Path -LiteralPath $payloadDirectory).Path -ne [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'installer\payload'))) { throw 'Unexpected payload directory.' }
    Remove-Item -LiteralPath $payloadDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $payloadDirectory | Out-Null

$projects = @(
    'src/BilisPDF.Desktop/BilisPDF.Desktop.csproj',
    'src/BilisPDF.Cli/BilisPDF.Cli.csproj',
    'src/BilisPDF.Shell/BilisPDF.Shell.csproj'
)

foreach ($project in $projects) {
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    dotnet publish (Join-Path $repositoryRoot $project) -c $Configuration -r win-x64 --self-contained $selfContained -p:PublishSingleFile=false -o $payloadDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}

$shellPackage = Join-Path $PSScriptRoot "bin\x64\$Configuration\BilisPDF.ShellPackage.msix"
& (Join-Path $PSScriptRoot 'modern-shell\build.ps1') -Configuration $Configuration -ExternalPayload $payloadDirectory -PackageOutput $shellPackage -Publisher $publisher

$registerModernShell = 'false'
if ($signRelease) {
    $binaries = @(Get-ChildItem -LiteralPath $payloadDirectory -File | Where-Object { $_.Name -like 'BilisPDF*' -and $_.Extension -in @('.exe', '.dll') } | Select-Object -ExpandProperty FullName)
    Invoke-ReleaseSigning -Paths ($binaries + $shellPackage) -Thumbprint $env:BILISPDF_SIGNING_CERT_THUMBPRINT -TimestampUrl $env:BILISPDF_TIMESTAMP_URL
    Copy-Item -LiteralPath $shellPackage -Destination $payloadDirectory
    $registerModernShell = 'true'
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $payloadDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'TERMS.md') -Destination $payloadDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $payloadDirectory

dotnet build (Join-Path $PSScriptRoot 'BilisPDF.Installer.wixproj') -c $Configuration -p:RegisterModernShell=$registerModernShell
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed.' }
$installer = Join-Path $PSScriptRoot "bin\x64\$Configuration\BilisPDF-Setup.msi"
if ($signRelease) {
    Invoke-ReleaseSigning -Paths $installer -Thumbprint $env:BILISPDF_SIGNING_CERT_THUMBPRINT -TimestampUrl $env:BILISPDF_TIMESTAMP_URL
}
$checksums = "{0} *{1}" -f (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash, (Split-Path -Leaf $installer)
$checksums | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $installer) 'SHA256SUMS.txt') -Encoding ascii
if ($registerModernShell -eq 'false') {
    Write-Warning 'Unsigned build: the Windows 11 menu requires Developer Mode. Public releases must sign the shell package; the classic menu remains available on other machines.'
}
