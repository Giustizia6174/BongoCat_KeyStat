@echo off
rem Adds the key-stats module to Bongo Cat (official or already-modded Assembly-CSharp.dll).
rem Optional: drag the game folder onto this file if the Steam install is not found automatically.
tasklist | find /i "BongoCat.exe" > nul
if not errorlevel 1 (
  echo Please quit Bongo Cat first.
  pause
  exit /b 1
)
dotnet "%~dp0Patcher.dll" %1
if errorlevel 1 (
  echo.
  echo Patch failed. Nothing was changed unless a message above says otherwise.
)
echo.
pause
