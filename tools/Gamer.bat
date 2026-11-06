@echo off
rem Starts the Gamer API (if it is not already running) and opens the Gamer desktop app.
rem When the app closes, the API is stopped only if this script started it.
setlocal
cd /d "%~dp0"

set "HEALTH_URL=http://127.0.0.1:5199/health"
set "STARTED_API="
rem First run on a PC: the stores are read with Chromium (Playwright), which must be installed once.
if not exist "%LOCALAPPDATA%\ms-playwright\chromium-*" (
    echo Installing browser component for the stores ^(first run only^)...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Api\playwright.ps1" install chromium
)


curl.exe -s -f -o nul --max-time 1 %HEALTH_URL% && goto :open_app

powershell -NoProfile -Command "Start-Process -FilePath '%~dp0Api\Gamer.Api.exe' -WorkingDirectory '%~dp0Api' -WindowStyle Hidden"
set "STARTED_API=1"

for /l %%i in (1,1,60) do (
    curl.exe -s -f -o nul --max-time 1 %HEALTH_URL% && goto :open_app
    ping -n 2 127.0.0.1 >nul
)

echo The Gamer API did not start. Check the Api folder.
pause
exit /b 1

:open_app
start /wait "" "%~dp0App\Gamer.exe"

if defined STARTED_API taskkill /im Gamer.Api.exe /f >nul 2>&1
endlocal
