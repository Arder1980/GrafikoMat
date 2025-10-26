@echo off
setlocal enabledelayedexpansion

REM Dodaj tylko sciezki Node.js i npm do istniejacego PATH (nie ruszaj reszty!)
if exist "%ProgramFiles%\nodejs" set "PATH=%PATH%;%ProgramFiles%\nodejs"
if exist "%APPDATA%\npm" set "PATH=%PATH%;%APPDATA%\npm"

echo ========================================
echo Claude Code - Setup i Launcher
echo ========================================
echo.

REM Sprawdz czy jestesmy w trybie setup czy launcher
if "%1"=="--setup" goto :setup
if "%1"=="--force-setup" goto :setup

REM Sprawdz czy Claude jest zainstalowany
echo [1/3] Sprawdzam instalacje Claude...
echo.

REM Najpierw sprawdz czy plik istnieje
if exist "%APPDATA%\npm\claude.cmd" (
    echo [OK] Claude jest zainstalowany
    echo.
) else (
    echo [!] Claude nie jest zainstalowany
    echo.
    pause
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
    echo.
    echo Anulowano. Aby uruchomic setup pozniej, uzyj: %~nx0 --setup
    pause
    exit /b
)

echo [2/3] Sprawdzam Git Bash...
echo.

REM Sprawdz czy Git Bash jest dostepny
if exist "C:\Program Files\Git\bin\bash.exe" (
    echo [OK] Git Bash jest zainstalowany
    echo.
) else (
    echo [!] Git Bash nie jest zainstalowany
    echo.
    pause
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
)

echo [3/3] Wszystko gotowe!
echo.

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

REM Sprawdz czy node dziala
node --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Node.js jest zainstalowany
    node --version
) else (
    REM Sprawdz czy node istnieje w typowej lokalizacji
    if exist "%ProgramFiles%\nodejs\node.exe" (
        echo [UWAGA] Node.js jest zainstalowany, ale nie widoczny w PATH
        echo.
        echo Musisz ZRESTARTOWAC KOMPUTER aby PATH zostal zaladowany!
        echo (lub zamknij WSZYSTKIE okna CMD/PowerShell i uruchom ponownie)
        echo.
        pause
        exit /b
    ) else (
        echo [!] Node.js NIE jest zainstalowany
        echo.
        echo Pobieranie Node.js...
        echo Otwieram strone pobierania. Pobierz i zainstaluj LTS version.
        echo.
        echo WAZNE: Po instalacji ZRESTARTUJ KOMPUTER!
        echo.
        start https://nodejs.org/en/download/
        pause
        exit /b
    )
)

echo.
echo ========================================
echo Krok 2/4: Sprawdzanie Git for Windows
echo ========================================
git --version >nul 2>&1
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
bash --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Git Bash jest dostepny
    bash --version | findstr /C:"version"
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

REM Sprawdz czy claude.cmd istnieje
if exist "%APPDATA%\npm\claude.cmd" (
    echo [OK] Claude Code jest zainstalowany
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
wt.exe --version >nul 2>&1
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
echo 3. Uruchom ponownie %~nx0
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
echo [8] DEBUG - Pokaz szczegoly instalacji
echo [0] Wyjscie
echo.
set /p choice="Wybierz opcje (0-8): "

cd /d "%~dp0"

if "%choice%"=="1" (
    echo.
    echo Uruchamiam Claude...
    echo.
    start "Claude Code" cmd /k "claude"
    exit /b
)

if "%choice%"=="2" (
    echo.
    echo TRYB AUTONOMICZNY - pliki beda zmieniane automatycznie!
    ping localhost -n 3 >nul
    start "Claude Code - Autonomous" cmd /k "claude --dangerously-skip-permissions"
    exit /b
)

if "%choice%"=="3" (
    echo.
    echo Uruchamiam z debugowaniem MCP...
    echo.
    start "Claude Code - MCP Debug" cmd /k "claude --mcp-debug"
    exit /b
)

if "%choice%"=="4" (
    echo.
    echo === Status instalacji ===
    echo.
    echo Node.js:
    node --version 2>nul || echo [!] Nie zainstalowane
    echo.
    echo npm:
    npm --version 2>nul || echo [!] Nie zainstalowane
    echo.
    echo Git:
    git --version 2>nul || echo [!] Nie zainstalowane
    echo.
    echo Git Bash:
    bash --version 2>nul | findstr /C:"version" || echo [!] Nie dostepne w PATH
    echo.
    echo Claude Code:
    claude --version 2>nul || echo [!] Nie zainstalowane
    echo.
    echo Windows Terminal:
    wt.exe --version >nul 2>&1 && echo [OK] Zainstalowane || echo [!] Nie zainstalowane
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

if "%choice%"=="8" (
    cls
    echo ========================================
    echo DEBUG - Szczegoly instalacji
    echo ========================================
    echo.
    echo === Lokalizacje plikow: ===
    echo.
    echo Node.js:
    if exist "%ProgramFiles%\nodejs\node.exe" (
        echo [OK] %ProgramFiles%\nodejs\node.exe
    ) else (
        echo [!] Nie znaleziono
    )
    echo.
    echo npm global:
    if exist "%APPDATA%\npm" (
        echo [OK] %APPDATA%\npm
        echo Pliki Claude:
        dir /B "%APPDATA%\npm\claude*" 2>nul || echo [!] Brak
    ) else (
        echo [!] Katalog nie istnieje
    )
    echo.
    echo Git Bash:
    if exist "C:\Program Files\Git\bin\bash.exe" (
        echo [OK] C:\Program Files\Git\bin\bash.exe
    ) else (
        echo [!] Nie znaleziono
    )
    echo.
    echo === Wersje: ===
    echo.
    echo Node.js:
    node --version 2>&1
    echo.
    echo npm:
    npm --version 2>&1
    echo.
    echo Git:
    git --version 2>&1
    echo.
    echo Claude Code:
    claude --version 2>&1
    echo.
    echo Bash:
    bash --version 2>&1 | findstr /C:"version"
    echo.
    echo === Aktualny PATH: ===
    echo %PATH%
    echo.
    pause
    goto :launcher
)

if "%choice%"=="0" (
    exit /b
)

echo.
echo Nieprawidlowa opcja!
ping localhost -n 2 >nul
goto :launcher