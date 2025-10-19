@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "ROOT_DIR=%~dp0"
set "PS1=%ROOT_DIR%ExportProject.ps1"
set "LOG=%ROOT_DIR%ExportProject.lastlog.txt"
set "SNAPSHOT=%ROOT_DIR%ProjektSnapshot_utf8.txt"

REM Generowanie snapshotu
set "PSCORE=%ProgramFiles%\PowerShell\7\pwsh.exe"
set "PSWIN=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if exist "%PSCORE%" (
  "%PSCORE%" -NoProfile -ExecutionPolicy Bypass -Command "& '%PS1%' -Root '%ROOT_DIR%' -OutputFile '%SNAPSHOT%'" 1> "%LOG%" 2>&1
) else (
  "%PSWIN%" -NoProfile -ExecutionPolicy Bypass -Command "& '%PS1%' -Root '%ROOT_DIR%' -OutputFile '%SNAPSHOT%'" 1> "%LOG%" 2>&1
)

if not "%ERRORLEVEL%"=="0" (
  echo [BLAD] Generowanie snapshotu niepomyslne. Zobacz: %LOG%
  exit /b 1
)

REM Aktualizacja Claude Project (ciche, bez outputu)
cd /d "%ROOT_DIR%"
python -m claude-pyrojects.cli update >nul 2>&1

if not "%ERRORLEVEL%"=="0" (
  echo [BLAD] Aktualizacja Claude Project niepomyslna
  exit /b 1
)

echo [OK] Projekt zaktualizowany: %SNAPSHOT%
exit /b 0