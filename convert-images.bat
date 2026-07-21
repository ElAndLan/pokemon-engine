@echo off
REM ===========================================================================
REM  Creature Game Maker - sprite transparency helper
REM  Reads raw generated sheets from raw_images\Raw, keys out the magenta
REM  background, and writes engine-ready grid-aligned RGBA sheets to
REM  raw_images\Converted.
REM  Originals in raw_images\Raw are never modified.
REM  Double-click this file, or run it from a terminal in the repo root.
REM
REM  The actual work lives in normalize-sprites.ps1 - edit FRAME, PAD, FUZZ and
REM  ERODE there. This file only launches it.
REM ===========================================================================

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0normalize-sprites.ps1" %*
if errorlevel 1 (
    echo.
    pause
    goto :eof
)

echo Opening the output folder...
start "" "%~dp0raw_images\Converted"
pause
