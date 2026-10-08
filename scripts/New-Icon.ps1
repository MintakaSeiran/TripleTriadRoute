# 独立した幾何学アイコン。上流画像やゲーム素材を使用しない。
Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new(256,256)
$canvas = [System.Drawing.Graphics]::FromImage($bitmap)
$canvas.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.Clear([System.Drawing.ColorTranslator]::FromHtml('#15283F'))
$card = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F1F5F9'))
$route = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#21B8A6'),12)
$node = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#EAB65B'))
$canvas.FillRectangle($card,39,49,57,88)
$canvas.FillRectangle($card,105,36,57,88)
$canvas.FillRectangle($card,171,49,46,88)
$points = [System.Drawing.Point[]]@([System.Drawing.Point]::new(52,187),[System.Drawing.Point]::new(110,162),[System.Drawing.Point]::new(154,192),[System.Drawing.Point]::new(205,163))
$canvas.DrawLines($route,$points)
foreach ($point in $points) { $canvas.FillEllipse($node,$point.X-10,$point.Y-10,20,20) }
$bitmap.Save((Join-Path $PSScriptRoot '../TripleTriadRoute/Images/Icon.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$node.Dispose(); $route.Dispose(); $card.Dispose(); $canvas.Dispose(); $bitmap.Dispose()
