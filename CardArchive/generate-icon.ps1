$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path "CardArchive/Assets" | Out-Null
$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$rect = New-Object System.Drawing.Rectangle(8, 8, 240, 240)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 42
$path.AddArc(8, 8, $r, $r, 180, 90); $path.AddArc(248-$r, 8, $r, $r, 270, 90)
$path.AddArc(248-$r, 248-$r, $r, $r, 0, 90); $path.AddArc(8, 248-$r, $r, $r, 90, 90); $path.CloseFigure()
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(18,48,91), [System.Drawing.Color]::FromArgb(18,132,222), 45)
$g.FillPath($bg, $path)
$white = [System.Drawing.Brushes]::White
$card = New-Object System.Drawing.Rectangle(43, 55, 170, 116)
$g.FillRectangle($white, 48, 60, 160, 108)
$blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(31,87,151))
$g.FillRectangle($blue, 59, 72, 49, 82)
$g.FillEllipse($white, 73, 82, 21, 21)
$g.FillEllipse($white, 67, 108, 33, 28)
$line = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(42,128,205))
$g.FillRectangle($line, 120, 84, 70, 10)
$g.FillRectangle($line, 120, 105, 58, 9)
$g.FillRectangle($line, 120, 126, 68, 9)
$drawer = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210,235,250))
$g.FillRectangle($drawer, 38, 177, 180, 35)
$g.FillRectangle($blue, 51, 185, 25, 20)
$g.FillEllipse($white, 57, 187, 8, 8)
$g.FillRectangle($white, 86, 187, 115, 5)
$g.FillRectangle($white, 86, 198, 80, 5)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120,210,255), 5)
$g.DrawArc($pen, 25, 25, 206, 206, 205, 130)
$bmp.Save("CardArchive/Assets/CardArchive-icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$stream = [System.IO.File]::Create("CardArchive/Assets/CardArchive.ico")
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
[CardArchive.NativeMethods] | Out-Null
$g.Dispose(); $bmp.Dispose(); $path.Dispose(); $bg.Dispose(); $blue.Dispose(); $line.Dispose(); $drawer.Dispose(); $pen.Dispose()
