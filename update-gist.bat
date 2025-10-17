@echo off
echo ========================================
echo   Aktualizacja Gista GrafikoMat
echo ========================================
echo.

REM Uruchom skrypt PowerShell
powershell -ExecutionPolicy Bypass -File "%~dp0update-gist.ps1"

echo.
echo ========================================
echo   Gotowe!
echo ========================================
pause