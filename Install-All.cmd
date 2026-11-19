@echo off
setlocal
cd /d "%~dp0"
echo Melvor Idle 2 Early Access - All-in-one Mod Pack v0.2.5.1
echo The game-folder window supports pasted paths and a modern Browse dialog.
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-All.ps1" %*
if errorlevel 1 (
  echo.
  echo Installation did not finish. Read the error above; it stops before installing if compilation fails.
) else (
  echo.
  echo Prelaunch setup completed. Start Melvor2Modded.exe to test.
)
echo.
pause
