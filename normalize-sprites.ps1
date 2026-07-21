<#
    Creature Game Maker - sprite sheet normaliser

    Turns a raw generated battle sheet into an engine-ready, grid-aligned RGBA
    sprite sheet:

      1. keys out the magenta background and trims the blend fringe
      2. finds the gap between the two views and splits them
      3. scales BOTH views by one shared factor (preserving relative scale)
      4. composes them into a 2*FRAME x FRAME sheet, flush cells, shared baseline

    Point sampling throughout - no smoothing filters, per VISUAL_MEMORY.md
    ("integer, nearest-neighbor").

    Run via convert-images.bat, or directly:
        powershell -File normalize-sprites.ps1 -Frame 64
#>
[CmdletBinding()]
param(
    [int]    $Frame  = 64,          # cell size; 2 cells => sheet is 2*Frame x Frame
    [int]    $Pad    = 2,           # transparent margin inside each cell
    [string] $Key    = '#FF00FF',   # transparency key colour
    [string] $Fuzz   = '25%',       # tolerance - generated backgrounds drift off the key
    [int]    $Erode  = 1,           # alpha shrink, kills the outline/background blend ring
    [string] $InDir,
    [string] $OutDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $InDir)  { $InDir  = Join-Path $root 'raw_images\Raw' }
if (-not $OutDir) { $OutDir = Join-Path $root 'raw_images\Converted' }

# --- locate ImageMagick ------------------------------------------------------
$magick = (Get-Command magick -ErrorAction SilentlyContinue).Source
if (-not $magick) {
    $magick = Get-ChildItem 'C:\Program Files\ImageMagick-*\magick.exe' -ErrorAction SilentlyContinue |
              Select-Object -First 1 -ExpandProperty FullName
}
if (-not $magick) {
    Write-Host 'ERROR: ImageMagick not found.'
    Write-Host 'Install with:  winget install ImageMagick.ImageMagick'
    Write-Host 'Then open a NEW terminal and run this again.'
    exit 1
}

foreach ($d in @($InDir, $OutDir)) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Force $d | Out-Null } }

$work = Join-Path ([IO.Path]::GetTempPath()) ("cgm_norm_" + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Force $work | Out-Null

function Get-Size([string]$p) {
    $wh = (& $magick identify -format '%w %h' $p) -split ' '
    [pscustomobject]@{ W = [int]$wh[0]; H = [int]$wh[1] }
}

# Column occupancy of the alpha channel, as a 0..255 value per source column.
# Zero means the column is fully transparent - i.e. a gap between the views.
function Get-ColumnAlpha([string]$p, [int]$w) {
    $txt = & $magick $p -alpha extract -resize "${w}x1!" -depth 8 txt:-
    $vals = New-Object int[] $w
    foreach ($line in ($txt | Select-Object -Skip 1)) {
        if ($line -match '^(\d+),0:\s*\((\d+)') { $vals[[int]$Matches[1]] = [int]$Matches[2] }
    }
    return $vals
}

# Split point = centre of the widest all-transparent column run nearest the middle.
# Falls back to the midpoint when no gap exists (views touching).
function Get-SplitX([int[]]$cols) {
    $w = $cols.Count; $mid = [int]($w / 2)
    $best = $null; $bestScore = [int]::MaxValue
    $i = 0
    while ($i -lt $w) {
        if ($cols[$i] -eq 0) {
            $s = $i
            while ($i -lt $w -and $cols[$i] -eq 0) { $i++ }
            $e = $i - 1
            # ignore the transparent margins at the far left/right of the sheet
            if ($s -gt 0 -and $e -lt ($w - 1)) {
                $c = [int](($s + $e) / 2)
                $score = [Math]::Abs($c - $mid)
                if ($score -lt $bestScore) { $bestScore = $score; $best = $c }
            }
        } else { $i++ }
    }
    if ($null -eq $best) { return $mid }
    return $best
}

$ok = 0; $fail = 0
Write-Host ''
Write-Host "Reading from : $InDir"
Write-Host "Writing to   : $OutDir"
Write-Host "Target       : $(2*$Frame) x $Frame  (2 cells of ${Frame}px)"
Write-Host ''

$files = Get-ChildItem $InDir -File | Where-Object { $_.Extension -match '^\.(png|jpg|jpeg|webp|bmp)$' }

foreach ($f in $files) {
    $name = $f.BaseName
    try {
        $keyed = Join-Path $work "$name.keyed.png"

        # 1. key out the background, shrink alpha to remove the blend fringe
        & $magick $f.FullName -fuzz $Fuzz -transparent $Key `
                  -channel A -morphology Erode "Octagon:$Erode" +channel `
                  -trim +repage $keyed
        if ($LASTEXITCODE -ne 0) { throw 'keying failed' }

        $size = Get-Size $keyed
        if ($size.W -lt 2 -or $size.H -lt 2) { throw 'nothing left after keying (is the background the right colour?)' }

        # 2. split into the two views on the transparent gap
        $splitX = Get-SplitX (Get-ColumnAlpha $keyed $size.W)
        $halves = @(
            @{ Tag = 'front'; Geom = "$splitX x$($size.H)+0+0"          -replace ' ','' },
            @{ Tag = 'back' ; Geom = "$($size.W-$splitX)x$($size.H)+$splitX+0" }
        )

        $parts = @()
        foreach ($h in $halves) {
            $p = Join-Path $work "$name.$($h.Tag).png"
            & $magick $keyed -crop $h.Geom +repage -trim +repage $p
            if ($LASTEXITCODE -ne 0) { throw "crop failed ($($h.Tag))" }
            $s = Get-Size $p
            if ($s.W -lt 1 -or $s.H -lt 1) { throw "empty $($h.Tag) view after split" }
            $parts += [pscustomobject]@{ Path = $p; W = $s.W; H = $s.H }
        }

        # 3. ONE shared scale factor, driven by whichever view is largest, so the
        #    two views keep their relative scale. Scaling them independently
        #    would silently break cross-view consistency.
        $box   = $Frame - (2 * $Pad)
        $maxW  = ($parts | Measure-Object -Property W -Maximum).Maximum
        $maxH  = ($parts | Measure-Object -Property H -Maximum).Maximum
        $scale = [Math]::Min($box / $maxW, $box / $maxH)

        $cells = @()
        foreach ($p in $parts) {
            $tw = [Math]::Max(1, [int][Math]::Round($p.W * $scale))
            $th = [Math]::Max(1, [int][Math]::Round($p.H * $scale))
            $cell = Join-Path $work ("$name." + [IO.Path]::GetFileNameWithoutExtension($p.Path) + ".cell.png")
            # -filter Point: nearest-neighbour, no blending introduced
            & $magick $p.Path -filter Point -resize "${tw}x${th}!" `
                      -background none -gravity South -extent "${Frame}x$($Frame - $Pad)" `
                      -background none -gravity North -extent "${Frame}x${Frame}" $cell
            if ($LASTEXITCODE -ne 0) { throw 'cell composition failed' }
            $cells += $cell
        }

        # 4. flush 1x2 grid, front left / back right
        $out = Join-Path $OutDir "$name.png"
        & $magick $cells[0] $cells[1] +append -background none -alpha on PNG32:$out
        if ($LASTEXITCODE -ne 0) { throw 'append failed' }

        $final = Get-Size $out
        if ($final.W -ne (2*$Frame) -or $final.H -ne $Frame) {
            throw "wrong output size $($final.W)x$($final.H)"
        }

        Write-Host ("  [ok]     {0}  ->  {1}x{2}" -f $f.Name, $final.W, $final.H)
        $ok++
    }
    catch {
        Write-Host ("  [FAILED] {0}  - {1}" -f $f.Name, $_.Exception.Message)
        $fail++
    }
}

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($ok -eq 0 -and $fail -eq 0) {
    Write-Host "Nothing to convert - $InDir is empty."
    Write-Host 'Save your generated sprite sheets into that folder, then run this again.'
} else {
    Write-Host "Done: $ok converted, $fail failed."
    if ($fail -gt 0) {
        Write-Host ''
        Write-Host 'Originals were not modified, so nothing is lost.'
    }
}
exit 0
