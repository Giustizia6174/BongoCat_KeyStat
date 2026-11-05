@echo off
rem Puts back the Assembly-CSharp.dll saved before patching and removes the key-stats module.
tasklist | find /i "BongoCat.exe" > nul
if not errorlevel 1 (
  echo Please quit Bongo Cat first.
  pause
  exit /b 1
)
dotnet "%~dp0Patcher.dll" %1 --restore
echo.
pause
