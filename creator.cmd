@echo off
rem Launch the Creator app from the repo root. Double-click in Explorer, or run
rem   creator            (from a terminal in this folder)
rem   .\creator.cmd      (from PowerShell)
rem Uses the D: SDK (this machine keeps .NET 10 there because C: is full);
rem falls back to whatever dotnet is on PATH.
setlocal
set "DOTNET=D:\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
"%DOTNET%" run --project "%~dp0src\Cgm.Creator" %*
endlocal
