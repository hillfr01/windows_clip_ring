# Regenerates app.ico: a white clipboard on a teal rounded square, drawn natively at each size.
# Usage: pwsh tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot '..\app.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function New-IconPng([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Designed on a 32-unit grid; snap to whole pixels so small sizes stay crisp.
    $f = $size / 32.0
    function S([float]$v) { [math]::Round($v * $f) }

    $teal = [System.Drawing.Color]::FromArgb(14, 124, 134)
    $dark = [System.Drawing.Color]::FromArgb(8, 70, 76)

    $bg = New-RoundedRect 0 0 $size $size ([math]::Max(2, (S 7)))
    $g.FillPath([System.Drawing.SolidBrush]::new($teal), $bg)

    $sheet = New-RoundedRect (S 7) (S 6) (S 18) (S 23) ([math]::Max(1, (S 2)))
    $g.FillPath([System.Drawing.Brushes]::White, $sheet)

    $clip = New-RoundedRect (S 11) (S 3) (S 10) (S 6) ([math]::Max(1, (S 2)))
    $g.FillPath([System.Drawing.SolidBrush]::new($dark), $clip)

    # Text lines on the sheet, the last one shorter.
    $lineBrush = [System.Drawing.SolidBrush]::new($teal)
    $lineH = [math]::Max(1, (S 2))
    $g.FillRectangle($lineBrush, (S 10), (S 13), (S 12), $lineH)
    $g.FillRectangle($lineBrush, (S 10), (S 18), (S 12), $lineH)
    $g.FillRectangle($lineBrush, (S 10), (S 23), (S 7), $lineH)

    $g.Dispose()
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO file: 6-byte header, 16-byte directory entry per image, then PNG data.
$images = foreach ($s in $sizes) { , (New-IconPng $s) }
$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $outPath)).Path + '\app.ico', $out.ToArray())
"Wrote $outPath ($($out.Length) bytes, sizes: $($sizes -join ', '))"
