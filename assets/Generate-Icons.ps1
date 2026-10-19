# Regenerate the app icon and package logos from the same resolution-independent drawing.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$drawing = [Windows.Media.DrawingGroup]::new()
$context = $drawing.Open()
$context.DrawRoundedRectangle([Windows.Media.BrushConverter]::new().ConvertFromString('#0F766E'), $null, [Windows.Rect]::new(8, 8, 240, 240), 54, 54)
$page = [Windows.Media.Geometry]::Parse('M 77,43 L 150,43 L 183,76 L 183,207 L 77,207 Z')
$context.DrawGeometry([Windows.Media.Brushes]::White, $null, $page)
$context.DrawGeometry([Windows.Media.BrushConverter]::new().ConvertFromString('#99D8CF'), $null, [Windows.Media.Geometry]::Parse('M 150,43 L 150,76 L 183,76 Z'))
$pen = [Windows.Media.Pen]::new([Windows.Media.BrushConverter]::new().ConvertFromString('#0F766E'), 12)
$pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
$context.DrawLine($pen, [Windows.Point]::new(101, 103), [Windows.Point]::new(147, 103))
$context.DrawLine($pen, [Windows.Point]::new(101, 129), [Windows.Point]::new(137, 129))
$context.DrawLine($pen, [Windows.Point]::new(101, 155), [Windows.Point]::new(147, 155))
$context.DrawEllipse([Windows.Media.BrushConverter]::new().ConvertFromString('#F97354'), $null, [Windows.Point]::new(181, 184), 40, 40)
$check = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White, 10)
$check.StartLineCap = $check.EndLineCap = [Windows.Media.PenLineCap]::Round
$context.DrawLine($check, [Windows.Point]::new(164, 184), [Windows.Point]::new(176, 196))
$context.DrawLine($check, [Windows.Point]::new(176, 196), [Windows.Point]::new(198, 173))
$context.Close()

$images = @{}
foreach ($size in @(16, 20, 24, 32, 40, 44, 48, 50, 64, 128, 150, 256)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
    $context.DrawDrawing($drawing)
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    $images[$size] = $stream.ToArray()
    $stream.Dispose()
}
foreach ($asset in @(@{Name='EasyPDF.png'; Size=256}, @{Name='StoreLogo.png'; Size=50}, @{Name='Square44x44Logo.png'; Size=44}, @{Name='Square150x150Logo.png'; Size=150})) {
    [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot $asset.Name), $images[$asset.Size])
}
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$stream = [IO.File]::Create((Join-Path $PSScriptRoot 'EasyPDF.ico'))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($size in $sizes) {
        $dimension = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$size].Length); $writer.Write([uint32]$offset)
        $offset += $images[$size].Length
    }
    foreach ($size in $sizes) { $writer.Write([byte[]]$images[$size]) }
}
finally { $writer.Dispose() }
