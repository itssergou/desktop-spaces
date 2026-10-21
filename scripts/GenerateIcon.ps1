$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconRoot = Split-Path -Parent $PSScriptRoot
$iconFile = Join-Path $iconRoot 'assets\DesktopSpaces.ico'
$iconImages = @()
foreach ($iconSize in @(16, 20, 24, 32, 40, 48, 64, 256)) {
    $iconBitmap = [Drawing.Bitmap]::new($iconSize, $iconSize)
    $iconGraphics = [Drawing.Graphics]::FromImage($iconBitmap)
    $iconGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $iconGraphics.ScaleTransform($iconSize / 64.0, $iconSize / 64.0)
    $iconBlue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#223a65'))
    $iconMint = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#75d9ce'), 4)
    $iconWhite = [Drawing.Pen]::new([Drawing.Color]::White, 4)
    try {
        $iconGraphics.FillRectangle($iconBlue, 2, 2, 60, 60)
        $iconGraphics.DrawRectangle($iconMint, 12, 12, 32, 28)
        $iconGraphics.FillRectangle($iconBlue, 22, 24, 30, 28)
        $iconGraphics.DrawRectangle($iconWhite, 22, 24, 30, 28)
        $iconStream = [IO.MemoryStream]::new()
        try {
            $iconBitmap.Save($iconStream, [Drawing.Imaging.ImageFormat]::Png)
            $iconImages += [pscustomobject]@{ Size = $iconSize; Data = $iconStream.ToArray() }
        } finally { $iconStream.Dispose() }
    } finally {
        $iconGraphics.Dispose(); $iconBitmap.Dispose(); $iconBlue.Dispose(); $iconMint.Dispose(); $iconWhite.Dispose()
    }
}
$iconWriter = [IO.BinaryWriter]::new([IO.File]::Create($iconFile))
try {
    $iconWriter.Write([uint16]0); $iconWriter.Write([uint16]1); $iconWriter.Write([uint16]$iconImages.Count)
    $iconOffset = 6 + 16 * $iconImages.Count
    foreach ($iconImage in $iconImages) {
        $iconDimension = if ($iconImage.Size -eq 256) { 0 } else { $iconImage.Size }
        $iconWriter.Write([byte]$iconDimension); $iconWriter.Write([byte]$iconDimension)
        $iconWriter.Write([byte]0); $iconWriter.Write([byte]0); $iconWriter.Write([uint16]1); $iconWriter.Write([uint16]32)
        $iconWriter.Write([uint32]$iconImage.Data.Length); $iconWriter.Write([uint32]$iconOffset)
        $iconOffset += $iconImage.Data.Length
    }
    foreach ($iconImage in $iconImages) { $iconWriter.Write([byte[]]$iconImage.Data) }
} finally { $iconWriter.Dispose() }
Write-Output $iconFile
