<#
.SYNOPSIS
  Build a standalone release of a Creature Game Maker project into releases\<version>\.

.DESCRIPTION
  Publishes the runtime self-contained (no .NET install needed on the target), then exports the
  project through Cgm.Tools so the release folder holds <GameName>.exe, game.cgmpack (with the
  project's assets embedded), config.json, and the bundled engine + native libraries.

  The version auto-increments: it finds the highest releases\X.Y.Z folder and bumps the patch
  component (starting at 0.0.1). Pass -Version to override, or -Major/-Minor to bump those instead.

.EXAMPLE
  ./scripts/build-release.ps1
  ./scripts/build-release.ps1 -Project samples/reedbank-hollow -Name "Reedbank Hollow"
  ./scripts/build-release.ps1 -Minor    # bump minor, reset patch (e.g. 0.1.0)
#>
[CmdletBinding()]
param(
  [string]$Project = "samples/reedbank-hollow",
  [string]$Name    = "Reedbank Hollow",
  [string]$Version,          # explicit version, e.g. "0.2.0"; overrides auto-increment
  [switch]$Major,            # bump major (X+1.0.0)
  [switch]$Minor,            # bump minor (X.Y+1.0)
  [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repo

# .NET 10 SDK lives on D: here (C: is full); prefer it, fall back to PATH.
$dotnet = if (Test-Path "D:\dotnet\dotnet.exe") { "D:\dotnet\dotnet.exe" } else { "dotnet" }

function Get-NextVersion {
  $releases = Join-Path $repo "releases"
  $latest = [version]"0.0.0"
  if (Test-Path $releases) {
    Get-ChildItem $releases -Directory -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
      ForEach-Object { $v = [version]$_.Name; if ($v -gt $latest) { $latest = $v } }
  }
  if     ($Major) { return "{0}.0.0" -f ($latest.Major + 1) }
  elseif ($Minor) { return "{0}.{1}.0" -f $latest.Major, ($latest.Minor + 1) }
  else            { return "{0}.{1}.{2}" -f $latest.Major, $latest.Minor, ($latest.Build + 1) }
}

if (-not $Version) { $Version = Get-NextVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' must be X.Y.Z." }

$outDir = Join-Path $repo "releases\$Version"
if (Test-Path $outDir) { throw "releases\$Version already exists. Bump the version or remove it." }

Write-Host "==> Building release $Version of '$Name' from $Project" -ForegroundColor Cyan

# 1) Self-contained runtime template (fresh, isolated).
$template = Join-Path ([System.IO.Path]::GetTempPath()) "cgm-template-$Version"
if (Test-Path $template) { Remove-Item $template -Recurse -Force }
Write-Host "==> Publishing self-contained runtime ($Runtime)..."
& $dotnet publish src/Cgm.Runtime -c Release -r $Runtime --self-contained true -o $template --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Runtime publish failed." }

# 2) Export the project onto that template (pack + config + renamed exe).
Write-Host "==> Exporting $Project -> releases\$Version"
& $dotnet run --project src/Cgm.Tools -- export $Project $outDir --name $Name --template $template
if ($LASTEXITCODE -ne 0) { throw "Export failed." }

# 3) Prove the standalone runs from its own pack (headless smoke).
$exe = Join-Path $outDir "$Name.exe"
if (-not (Test-Path $exe)) { throw "Expected $exe was not produced." }
Write-Host "==> Verifying the standalone build..."
& $exe --smoke
if ($LASTEXITCODE -ne 0) { throw "Standalone smoke run failed (exit $LASTEXITCODE)." }

Remove-Item $template -Recurse -Force -ErrorAction SilentlyContinue
$size = "{0:N0} MB" -f ((Get-ChildItem $outDir -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "==> Release $Version ready: $exe ($size)" -ForegroundColor Green
