@echo off
setlocal EnableExtensions

set "SELF_DIR=%~dp0"
set "PS1=%SELF_DIR%ExportProject.ps1"
set "LOG=%SELF_DIR%ExportProject.lastlog.txt"

if not exist "%PS1%" (
  echo [BLAD] Brak pliku: "%PS1%"
  pause
  exit /b 1
)

rem Preferuj PowerShell 7, w razie braku Windows PowerShell
set "PSCORE=%ProgramFiles%\PowerShell\7\pwsh.exe"
set "PSWIN=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if exist "%PSCORE%" (
  "%PSCORE%" -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %* 1> "%LOG%" 2>&1
) else (
  "%PSWIN%"  -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %* 1> "%LOG%" 2>&1
)

set "ERR=%ERRORLEVEL%"
if not "%ERR%"=="0" (
  echo [BLAD] Skrypt zakonczyl sie kodem %ERR%. Szczegoly w: "%LOG%"
  pause
  exit /b %ERR%
) else (
  rem Sukces -> okno zamyka sie natychmiast (bez pauzy)
  exit /b 0
)
