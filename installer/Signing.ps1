function Assert-ReleaseSignature {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $ExpectedThumbprint)
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint -or -not $signature.TimeStamperCertificate) {
        throw "Release signature verification failed for $Path. A trusted signature from the configured publisher and a timestamp are required."
    }
}

function Invoke-ReleaseSigning {
    param(
        [Parameter(Mandatory)][string[]] $Paths,
        [Parameter(Mandatory)][string] $Thumbprint,
        [Parameter(Mandatory)][string] $TimestampUrl
    )
    $timestamp = $null
    if (-not [Uri]::TryCreate($TimestampUrl, [UriKind]::Absolute, [ref]$timestamp) -or $timestamp.Scheme -notin @('http', 'https')) {
        throw 'Set BILISPDF_TIMESTAMP_URL to the RFC 3161 timestamp URL supplied by your certificate issuer.'
    }
    $signTool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter signtool.exe |
        Where-Object FullName -Match '\\x64\\signtool.exe$' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signTool) { throw 'Windows SDK signtool.exe was not found.' }
    foreach ($path in $Paths) {
        # Use the Windows certificate store; passwords and private keys never become command-line arguments.
        & $signTool sign /sha1 $Thumbprint /s My /fd SHA256 /tr $TimestampUrl /td SHA256 /d BilisPDF /du 'https://github.com/Arjunren/BilisPDF' $path
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $path" }
        & $signTool verify /pa /all /tw $path
        if ($LASTEXITCODE -ne 0) { throw "Windows could not verify $path" }
        Assert-ReleaseSignature -Path $path -ExpectedThumbprint $Thumbprint
    }
}
