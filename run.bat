@echo off
setlocal enabledelayedexpansion
REM ===========================================================================
REM  Creature Game Maker - run helper
REM  Uses the D:\dotnet SDK 10 install (see docs/TECH_STACK.md "Local dev env").
REM  Double-click this file, or run it from a terminal in the repo root.
REM ===========================================================================

set "DOTNET_ROOT=D:\dotnet"
set "NUGET_PACKAGES=D:\.nuget-packages"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_EXE=D:\dotnet\dotnet.exe"

REM Run from the folder this script lives in.
cd /d "%~dp0"

if not exist "%DOTNET_EXE%" (
    echo ERROR: .NET 10 SDK not found at %DOTNET_EXE%.
    echo See docs/TECH_STACK.md for the local dev environment setup.
    pause
    goto :eof
)

:menu
echo.
echo ============================================
echo    Creature Game Maker - run helper
echo ============================================
echo    [1] Creator app        (Avalonia UI)
echo    [2] Runtime window      (Silk.NET, --debug)
echo    [3] Build + test only
echo    [Q] Quit
echo ============================================
choice /c 123Q /n /m "Select an option: "

if errorlevel 4 goto :eof
if errorlevel 3 goto :buildtest
if errorlevel 2 goto :runtime
if errorlevel 1 goto :creator

:creator
echo.
echo Launching Creator app... (close the window to return)
"%DOTNET_EXE%" run --project src\Cgm.Creator
echo.
echo Creator exited with code %errorlevel%.
pause
goto :menu

:runtime
echo.
echo Launching Runtime window... (press Esc in the window to exit)
"%DOTNET_EXE%" run --project src\Cgm.Runtime -- --debug
echo.
echo Runtime exited with code %errorlevel%.
pause
goto :menu

:buildtest
echo.
echo Building and testing the full solution...
"%DOTNET_EXE%" build CreatureGameMaker.slnx
"%DOTNET_EXE%" test CreatureGameMaker.slnx
echo.
pause
goto :menu
