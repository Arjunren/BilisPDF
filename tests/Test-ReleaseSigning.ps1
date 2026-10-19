param([Parameter(Mandatory)][string] $UnsignedBinary)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer\Signing.ps1')
$rejected = $false
try { Assert-ReleaseSignature -Path $UnsignedBinary -ExpectedThumbprint ('0' * 40) }
catch { if ($_.Exception.Message -notlike 'Release signature verification failed*') { throw }; $rejected = $true }
if (-not $rejected) { throw 'An unsigned binary was accepted as a verified release.' }

$rejected = $false
try { Invoke-ReleaseSigning -Paths $UnsignedBinary -Thumbprint ('0' * 40) -TimestampUrl 'file:///invalid' }
catch { if ($_.Exception.Message -notlike 'Set BILISPDF_TIMESTAMP_URL*') { throw }; $rejected = $true }
if (-not $rejected) { throw 'An invalid timestamp endpoint was accepted.' }
Write-Host 'Release checks passed: unsigned binaries and invalid timestamp URLs are rejected.'
