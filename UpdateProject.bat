@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo   GrafikoMat - Aktualizacja Projektu
echo ========================================
echo.

set "ROOT_DIR=%~dp0"
set "PS1=%ROOT_DIR%ExportProject.ps1"
set "LOG=%ROOT_DIR%ExportProject.lastlog.txt"
set "SNAPSHOT=%ROOT_DIR%ProjektSnapshot_utf8.txt"

REM ========================================
REM KROK 1: Generowanie snapshotu
REM ========================================
echo [1/2] Generowanie snapshotu projektu...

if not exist "%PS1%" (
  echo [BLAD] Brak pliku: "%PS1%"
  pause
  exit /b 1
)

REM Preferuj PowerShell 7, w razie braku Windows PowerShell
set "PSCORE=%ProgramFiles%\PowerShell\7\pwsh.exe"
set "PSWIN=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

REM POPRAWKA: Użyj pojedynczych cudzysłowów wewnątrz PowerShell
if exist "%PSCORE%" (
  "%PSCORE%" -NoProfile -ExecutionPolicy Bypass -Command "& '%PS1%' -Root '%ROOT_DIR%' -OutputFile '%SNAPSHOT%'" 1> "%LOG%" 2>&1
) else (
  "%PSWIN%" -NoProfile -ExecutionPolicy Bypass -Command "& '%PS1%' -Root '%ROOT_DIR%' -OutputFile '%SNAPSHOT%'" 1> "%LOG%" 2>&1
)

set "ERR=%ERRORLEVEL%"
if not "%ERR%"=="0" (
  echo [BLAD] Generowanie snapshotu zakonczylo sie kodem %ERR%
  echo Szczegoly w: "%LOG%"
  type "%LOG%"
  pause
  exit /b %ERR%
)

echo [OK] Snapshot wygenerowany: %SNAPSHOT%
echo.

REM ========================================
REM KROK 2: Aktualizacja Claude Project
REM ========================================
echo [2/2] Aktualizacja Claude Project...

cd /d "%ROOT_DIR%"
py -m claude-pyrojects.cli update

set "ERR=%ERRORLEVEL%"
if not "%ERR%"=="0" (
  echo [BLAD] Aktualizacja Claude Project zakonczyla sie kodem %ERR%
  pause
  exit /b %ERR%
)

echo.
echo ========================================
echo   SUKCES!
echo ========================================
echo   1. Snapshot utworzony:
echo      %SNAPSHOT%
echo   2. Claude Project zaktualizowany
echo ========================================
echo.
echo Mozesz teraz otworzyc projekt w Claude.
echo.
pause
exit /b 0