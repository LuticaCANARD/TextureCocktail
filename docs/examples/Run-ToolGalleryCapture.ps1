<#
Rebuild seven English README comparison sheets with actual TextureCocktail previews.
Requires Windows, the matching Unity Editor, and its normal graphics device (no
-nographics). Close this project first; other Unity projects may remain open.
Raw shader readbacks are kept in Logs/ToolGalleryRaw. System.Drawing lays out the
comparison chart and labels without modifying effects. No desktop is captured;
no scenes, source assets, language preferences or product code are modified.
#>
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [string]$ProjectPath
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($ProjectPath)) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}
$fixtureFolder = Join-Path $ProjectPath ('Assets/ToolGalleryCapture_' + [guid]::NewGuid().ToString('N'))
$editorFolder = Join-Path $fixtureFolder 'Editor'
$logPath = Join-Path $ProjectPath 'Logs/ToolGalleryCapture.log'
try {
    New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path $logPath) -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'ToolGalleryCapture.cs') -Destination $editorFolder
    $arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $ProjectPath), '-force-d3d11',
        '-executeMethod', 'ToolGalleryCapture.Run', '-logFile', ('"{0}"' -f $logPath))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        if (Test-Path $logPath) { Get-Content $logPath -Tail 80 }
        throw "Tool gallery failed (exit $($process.ExitCode)). Log: $logPath"
    }
    $success = Select-String -Path $logPath -Pattern 'TEXTURE_COCKTAIL_TOOL_GALLERY_PASSED'
    if (-not $success) { throw "Unity exited without gallery completion. Log: $logPath" }
    $success | ForEach-Object { Write-Output $_.Line }
} finally {
    if (Test-Path $fixtureFolder) { Remove-Item -LiteralPath $fixtureFolder -Recurse -Force }
    if (Test-Path ($fixtureFolder + '.meta')) { Remove-Item -LiteralPath ($fixtureFolder + '.meta') -Force }
}

# This is a labeled comparison chart, not a screenshot of product UI.
Add-Type -AssemblyName System.Drawing
$manifestPath = Join-Path $ProjectPath 'Logs/ToolGalleryRaw/gallery.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.sheets.Count -ne 7) { throw 'Expected seven shader comparison sheets.' }
$outputFolder = Join-Path $ProjectPath 'docs/media'
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$resources = New-Object System.Collections.Generic.List[System.IDisposable]
function Make-Brush([int]$r,[int]$g,[int]$b) {
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r,$g,$b))
    $resources.Add($brush)
    return $brush
}
function Make-Font([single]$size,[bool]$bold) {
    $style = if ($bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = New-Object System.Drawing.Font('Segoe UI',$size,$style,[System.Drawing.GraphicsUnit]::Pixel)
    $resources.Add($font)
    return $font
}
$white = Make-Brush 245 248 250
$muted = Make-Brush 179 196 209
$accent = Make-Brush 94 199 212
$foot = Make-Brush 135 161 179
$titleFont = Make-Font 31 $true
$subtitleFont = Make-Font 18 $false
$paneFont = Make-Font 22 $true
$settingsFont = Make-Font 14 $false
$smallFont = Make-Font 13 $true
$footerFont = Make-Font 12 $false
$line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(51,69,79))
$resources.Add($line)
try {
    $sheetIndex=0
    foreach ($sheet in $manifest.sheets) {
        $bitmap = New-Object System.Drawing.Bitmap(1500,800)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(14,19,26))
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawString('TEXTURECOCKTAIL  /  TOOL GALLERY',$smallFont,$accent,36,24)
            $graphics.DrawString($sheet.title,$titleFont,$white,36,59)
            $graphics.DrawString($sheet.subtitle,$subtitleFont,$muted,36,107)
            $count = $sheet.panes.Count
            [single]$width = (1500-72-20*($count-1))/$count
            for($i=0;$i -lt $count;$i++) {
                [single]$x = 36+$i*($width+20)
                $pane = $sheet.panes[$i]
                $input = [System.Drawing.Image]::FromFile($pane.path)
                try {
                    if ($input.Width -ne 512 -or $input.Height -ne 512) { throw "Invalid shader readback: $($pane.path)" }
                    $rect = New-Object System.Drawing.RectangleF($x,160,$width,$width)
                    $graphics.DrawImage($input,$rect)
                } finally { $input.Dispose() }
                $graphics.DrawString($pane.title,$paneFont,$white,$x,(170+$width))
                $settingsRect = New-Object System.Drawing.RectangleF($x,(207+$width),$width,50)
                $graphics.DrawString($pane.settings,$settingsFont,$muted,$settingsRect)
            }
            $graphics.DrawLine($line,36,751,1464,751)
            $graphics.DrawString('Actual TextureCocktail shader previews  /  Procedural inputs  /  English comparison sheet',$footerFont,$foot,36,764)
            $graphics.DrawString(('{0:00} / 07' -f ($sheetIndex+1)),$footerFont,$foot,1400,764)
            $destination = Join-Path $outputFolder ($sheet.file+'.png')
            $bitmap.Save($destination,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output "Wrote $destination"
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
        $sheetIndex++
    }
} finally { foreach($resource in $resources) { $resource.Dispose() } }
