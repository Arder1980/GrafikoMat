@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

echo ========================================
echo Claude Code - Setup i Launcher
echo ========================================
echo.

REM Sprawdz czy jestesmy w trybie setup czy launcher
if "%1"=="--setup" goto :setup
if "%1"=="--force-setup" goto :setup

REM Sprawdz czy Claude jest zainstalowany
where claude >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo UWAGA: Claude nie jest zainstalowany!
    echo.
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
    echo.
    echo Anulowano. Aby uruchomic setup pozniej, uzyj: claude.bat --setup
    pause
    exit /b
)

REM Sprawdz czy Git Bash jest dostepny
where bash >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo UWAGA: Git Bash nie jest dostepny w PATH!
    echo Claude Code wymaga Git Bash do dzialania.
    echo.
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
)

REM Wszystko OK, pokaz menu launcher
goto :launcher

:setup
cls
echo ========================================
echo Claude Code - INSTALATOR
echo ========================================
echo.
echo Ten skrypt zainstaluje:
echo [1] Node.js (jesli brak)
echo [2] Git for Windows (jesli brak)
echo [3] Claude Code CLI
echo [4] Windows Terminal (opcjonalnie)
echo.
echo Instalacja wymaga uprawnien administratora dla niektorych krokow.
echo.
pause

echo.
echo ========================================
echo Krok 1/4: Sprawdzanie Node.js
echo ========================================
where node >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Node.js jest zainstalowany
    node --version
) else (
    echo [!] Node.js NIE jest zainstalowany
    echo.
    echo Pobieranie Node.js...
    echo Otwieram strone pobierania. Pobierz i zainstaluj LTS version.
    echo Po instalacji ZRESTARTUJ ten skrypt.
    start https://nodejs.org/en/download/
    pause
    exit /b
)

echo.
echo ========================================
echo Krok 2/4: Sprawdzanie Git for Windows
echo ========================================
where git >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Git jest zainstalowany
    git --version
) else (
    echo [!] Git NIE jest zainstalowany
    echo.
    echo Pobieranie Git for Windows...
    echo Otwieram strone pobierania.
    echo WAZNE: Podczas instalacji wybierz "Git from command line and 3rd-party software"
    echo Po instalacji ZRESTARTUJ komputer.
    start https://git-scm.com/download/win
    pause
    exit /b
)

echo.
echo ========================================
echo Krok 3/4: Sprawdzanie Git Bash w PATH
echo ========================================
where bash >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Git Bash jest dostepny
    bash --version
) else (
    echo [!] Git Bash NIE jest w PATH
    echo.
    echo Dodaje Git Bash do PATH...
    
    REM Sprawdz typowe lokalizacje
    set "git_bin="
    if exist "C:\Program Files\Git\bin\bash.exe" set "git_bin=C:\Program Files\Git\bin"
    if exist "C:\Program Files (x86)\Git\bin\bash.exe" set "git_bin=C:\Program Files (x86)\Git\bin"
    
    if defined git_bin (
        echo Znaleziono Git Bash w: !git_bin!
        echo.
        echo Aby dodac do PATH, uruchom jako ADMINISTRATOR:
        echo.
        echo setx PATH "%%PATH%%;!git_bin!" /M
        echo.
        echo Lub dodaj recznie przez: Panel sterowania ^> System ^> Zaawansowane ^> Zmienne srodowiskowe
        echo.
        pause
    ) else (
        echo Nie znaleziono Git Bash. Upewnij sie ze Git jest poprawnie zainstalowany.
        pause
        exit /b
    )
)

echo.
echo ========================================
echo Krok 4/4: Instalacja Claude Code
echo ========================================
where claude >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Claude Code jest zainstalowany
    claude --version
    echo.
    set /p update="Czy chcesz zaktualizowac do najnowszej wersji? (tak/nie): "
    if /i "!update!"=="tak" goto :install_claude
    if /i "!update!"=="t" goto :install_claude
) else (
    echo [!] Claude Code NIE jest zainstalowany
    goto :install_claude
)
goto :post_setup

:install_claude
echo.
echo Instalowanie/Aktualizowanie Claude Code...
echo.
call npm install -g @anthropic-ai/claude-code
if %errorlevel% neq 0 (
    echo.
    echo BLAD podczas instalacji!
    echo Sprawdz czy masz uprawnienia (sprobuj uruchomic CMD jako administrator)
    pause
    exit /b
)
echo.
echo [OK] Claude Code zainstalowany pomyslnie!

:post_setup
echo.
echo ========================================
echo Krok opcjonalny: Windows Terminal
echo ========================================
where wt.exe >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Windows Terminal jest zainstalowany
) else (
    echo [!] Windows Terminal NIE jest zainstalowany
    echo.
    set /p wt="Czy chcesz zainstalowac Windows Terminal? (tak/nie): "
    if /i "!wt!"=="tak" (
        echo Otwieram Microsoft Store...
        start ms-windows-store://pdp/?ProductId=9N0DX20HK701
        echo.
        echo Zainstaluj z Microsoft Store i wroc tutaj.
        pause
    )
)

echo.
echo ========================================
echo INSTALACJA ZAKONCZONA!
echo ========================================
echo.
echo Co dalej:
echo 1. Zamknij to okno
echo 2. Otworz NOWY PowerShell/Terminal (aby zaladowac nowy PATH)
echo 3. Uruchom ponownie claude.bat
echo 4. Przy pierwszym uruchomieniu Claude zaloguj sie przez przegladarke
echo.
echo Jezeli bash nadal nie dziala, ZRESTARTUJ komputer.
echo.
pause
exit /b

:launcher
cls
echo ========================================
echo Claude - AI Coding Assistant
echo ========================================
echo.
echo Folder projektu: %~dp0
echo.
echo [1] Uruchom Claude (standardowo - z potwierdzeniami)
echo [2] Uruchom Claude (autonomicznie - BEZ potwierdzen)
echo [3] Uruchom z debugowaniem MCP
echo [4] Sprawdz wersje i status
echo [5] Wyloguj sie z Claude
echo [6] Wyczysc cache npm
echo [7] Uruchom ponownie INSTALATOR
echo [0] Wyjscie
echo.
set /p choice="Wybierz opcje (0-7): "

cd /d "%~dp0"

if "%choice%"=="1" (
    echo.
    echo Uruchamiam Claude...
    echo.
    powershell -NoExit -Command "Write-Host 'Claude gotowy' -ForegroundColor Green; claude"
    exit /b
)

if "%choice%"=="2" (
    echo.
    echo TRYB AUTONOMICZNY - pliki beda zmieniane automatycznie!
    timeout /t 2 > nul
    powershell -NoExit -Command "Write-Host 'Claude - tryb autonomiczny' -ForegroundColor Red; claude --dangerously-skip-permissions"
    exit /b
)

if "%choice%"=="3" (
    echo.
    powershell -NoExit -Command "Write-Host 'Claude - debug MCP' -ForegroundColor Yellow; claude --mcp-debug"
    exit /b
)

if "%choice%"=="4" (
    echo.
    echo === Status instalacji ===
    echo.
    echo Node.js:
    where node >nul 2>&1 && node --version || echo [!] Nie zainstalowane
    echo.
    echo Git:
    where git >nul 2>&1 && git --version || echo [!] Nie zainstalowane
    echo.
    echo Git Bash:
    where bash >nul 2>&1 && bash --version || echo [!] Nie dostepne w PATH
    echo.
    echo Claude Code:
    where claude >nul 2>&1 && claude --version || echo [!] Nie zainstalowane
    echo.
    echo Windows Terminal:
    where wt.exe >nul 2>&1 && echo [OK] Zainstalowane || echo [!] Nie zainstalowane
    echo.
    pause
    goto :launcher
)

if "%choice%"=="5" (
    echo.
    echo Wylogowywanie...
    claude --logout
    echo.
    pause
    goto :launcher
)

if "%choice%"=="6" (
    echo.
    echo Czyszczenie cache...
    npm cache clean --force
    echo [OK] Cache wyczyszczony
    echo.
    pause
    goto :launcher
)

if "%choice%"=="7" (
    goto :setup
)

if "%choice%"=="0" (
    exit /b
)

echo.
echo Nieprawidlowa opcja!
timeout /t 2 > nul
goto :launcher