[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [Parameter(Mandatory)]
    [string] $ExternalPayload,
    [Parameter(Mandatory)]
    [string] $PackageOutput
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$layout = Join-Path $repositoryRoot "artifacts\modern-shell\$Configuration\layout"
$nativeOutput = Join-Path $repositoryRoot "artifacts\shell-extension\$Configuration\EasyPDF.ShellExtension.dll"

$msbuild = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe -ErrorAction Stop |
    Where-Object FullName -Match '\\MSBuild\\Current\\Bin\\MSBuild.exe$' |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $msbuild) { throw 'Visual Studio MSBuild with the C++ workload was not found.' }

& $msbuild (Join-Path $repositoryRoot 'src\EasyPDF.ShellExtension\EasyPDF.ShellExtension.vcxproj') "/p:Configuration=$Configuration" '/p:Platform=x64' '/m'
if ($LASTEXITCODE -ne 0) { throw 'Building the native Explorer command failed.' }

Copy-Item -LiteralPath $nativeOutput -Destination $ExternalPayload -Force

if (Test-Path -LiteralPath $layout) {
    Remove-Item -LiteralPath $layout -Recurse -Force
}
New-Item -ItemType Directory -Path $layout -Force | Out-Null
$assets = Join-Path $ExternalPayload 'Assets'
if (Test-Path -LiteralPath $assets) {
    Remove-Item -LiteralPath $assets -Recurse -Force
}
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Destination $layout

Add-Type -AssemblyName System.Drawing
foreach ($asset in @(
    @{ Name = 'StoreLogo.png'; Size = 50 },
    @{ Name = 'Square44x44Logo.png'; Size = 44 },
    @{ Name = 'Square150x150Logo.png'; Size = 150 }
)) {
    $bitmap = [System.Drawing.Bitmap]::new($asset.Size, $asset.Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(19, 111, 99))
        $fontSize = [Math]::Max(10, [int]($asset.Size * 0.34))
        $font = [System.Drawing.Font]::new('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        try {
            $format = [System.Drawing.StringFormat]::new()
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $brush = [System.Drawing.Brushes]::White
            $graphics.DrawString('PDF', $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $asset.Size, $asset.Size), $format)
        }
        finally {
            $font.Dispose()
        }
        $bitmap.Save((Join-Path $assets $asset.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter makeappx.exe -ErrorAction Stop |
    Where-Object FullName -Match '\\x64\\makeappx.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $makeAppx) { throw 'Windows SDK makeappx.exe was not found.' }

$packageDirectory = Split-Path -Parent $PackageOutput
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
if (Test-Path -LiteralPath $PackageOutput) { Remove-Item -LiteralPath $PackageOutput -Force }
& $makeAppx pack /d $layout /p $PackageOutput /o /nv
if ($LASTEXITCODE -ne 0) { throw 'Building the sparse MSIX package failed.' }

if ($env:EASYPDF_SIGNING_CERT_PATH) {
    $signTool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter signtool.exe -ErrorAction Stop |
        Where-Object FullName -Match '\\x64\\signtool.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $signTool) { throw 'Windows SDK signtool.exe was not found.' }
    & $signTool sign /fd SHA256 /f $env:EASYPDF_SIGNING_CERT_PATH /p $env:EASYPDF_SIGNING_CERT_PASSWORD $PackageOutput
    if ($LASTEXITCODE -ne 0) { throw 'Signing the sparse MSIX package failed.' }
}
