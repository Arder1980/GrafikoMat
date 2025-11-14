@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Gemini CLI - Setup i Launcher
echo ========================================
echo.
REM Sprawdz czy jestesmy w trybie setup czy launcher
if "%1"=="--setup" goto :setup
if "%1"=="--force-setup" goto :setup

echo Sprawdzam status Gemini...
call gemini --version >nul 2>&1
set "gemini_status=%errorlevel%"
echo Gemini errorlevel: %gemini_status%

if %gemini_status% neq 0 (
    echo.
    echo UWAGA: Gemini CLI nie jest zainstalowany!
    echo.
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
    echo.
    echo Anulowano. Aby uruchomic setup pozniej, uzyj: Gemini_CLI.bat --setup
    pause
    exit /b
)

echo Sprawdzam status Bash...
call bash --version >nul 2>&1
set "bash_status=%errorlevel%"
echo Bash errorlevel: %bash_status%

if %bash_status% neq 0 (
    echo.
    echo UWAGA: Git Bash nie jest dostepny!
    echo Gemini CLI ^(i jego zaleznosci^) wymaga Git Bash do dzialania.
    echo.
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install!"=="tak" goto :setup
    if /i "!install!"=="t" goto :setup
)

echo.
echo Przechodze do menu launchera...
echo.
pause

REM Wszystko OK, pokaz menu launcher
goto :launcher

:setup
cls
echo ========================================
echo Gemini CLI - INSTALATOR
echo ========================================
echo.
echo Ten skrypt zainstaluje:
echo  1. Node.js - jesli brak
echo  2. Git for Windows - jesli brak
echo  3. Gemini CLI
echo  4. Windows Terminal - opcjonalnie
echo.
pause

REM === SPRAWDZANIE NODE.JS ===
:check_node
echo.
echo ========================================
echo Krok 1/4: Sprawdzanie Node.js
echo ========================================

node --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Node.js jest zainstalowany
    node --version
    goto :check_npm
)

REM Sprawdz czy jest fizycznie zainstalowany
if exist "C:\Program Files\nodejs\node.exe" (
    echo [UWAGA] Node.js jest zainstalowany ale nie dziala
    echo.
    echo Problem: PATH jest uszkodzony.
    echo Napraw PATH wedlug instrukcji i zrestartuj komputer.
    echo.
    pause
    exit /b
)

echo [UWAGA] Node.js NIE jest zainstalowany
echo.
echo Otwieram strone pobierania Node.js...
echo Po instalacji ZRESTARTUJ KOMPUTER
echo.
start https://nodejs.org/en/download/
pause
exit /b

REM === SPRAWDZANIE NPM ===
:check_npm
echo.
echo ========================================
echo Sprawdzanie npm
echo ========================================
echo.
echo Testuje npm...

call npm --version
set "npm_result=%errorlevel%"

echo Errorlevel: %npm_result%

if %npm_result% equ 0 (
    echo [OK] npm jest dostepne
    goto :check_git
)

echo [UWAGA] npm nie dziala
echo.
pause
exit /b

REM === SPRAWDZANIE GIT ===
:check_git
echo.
echo ========================================
echo Krok 2/4: Sprawdzanie Git for Windows
echo ========================================

git --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Git jest zainstalowany
    git --version
    goto :check_bash
)

if exist "C:\Program Files\Git\cmd\git.exe" (
    echo [UWAGA] Git jest zainstalowany ale nie dziala
    echo Problem: PATH jest uszkodzony.
    echo.
    pause
    exit /b
)

echo [UWAGA] Git NIE jest zainstalowany
echo.
echo Otwieram strone pobierania Git...
echo.
start https://git-scm.com/download/win
pause
exit /b

REM === SPRAWDZANIE BASH ===
:check_bash
echo.
echo ========================================
echo Krok 3/4: Sprawdzanie Git Bash
echo ========================================

bash --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Git Bash jest dostepny
    goto :check_gemini
)

if exist "C:\Program Files\Git\bin\bash.exe" (
    echo [UWAGA] Git Bash jest zainstalowany ale nie dziala
    echo Problem: PATH jest uskodzony.
    echo.
    pause
    exit /b
)

echo [UWAGA] Git Bash nie jest dostepny
echo.
pause
exit /b

REM === SPRAWDZANIE/INSTALACJA GEMINI ===
:check_gemini
echo.
echo ========================================
echo Krok 4/4: Instalacja Gemini CLI
echo ========================================
echo.
echo Sprawdzam czy Gemini CLI jest zainstalowany...

call gemini --version >nul 2>&1
set "gemini_result=%errorlevel%"

echo Errorlevel: %gemini_result%

if %gemini_result% equ 0 (
    echo.
    echo [OK] Gemini CLI jest juz zainstalowany
    call gemini --version
    echo.
    set /p update="Czy chcesz zaktualizowac? (tak/nie): "
    if /i "!update!"=="tak" goto :install_gemini
    if /i "!update!"=="t" goto :install_gemini
    goto :post_setup
)

echo.
echo [INFO] Gemini CLI NIE jest zainstalowany
echo Rozpoczynam instalacje...

:install_gemini
echo.
echo Instalowanie Gemini CLI przez npm...
echo To moze potrwac 1-2 minuty...
echo.
call npm install -g @google/gemini-cli

set "install_result=%errorlevel%"
echo.
echo Instalacja zakonczona z kodem: %install_result%

if %install_result% neq 0 (
    echo.
    echo [BLAD] Instalacja nie powiodla sie!
    echo.
    pause
    exit /b
)

echo.
echo [OK] Gemini CLI zainstalowany pomyslnie!

:post_setup
echo.
echo ========================================
echo Krok opcjonalny: Windows Terminal
echo ========================================

wt.exe --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Windows Terminal jest zainstalowany
    goto :setup_complete
)

echo [INFO] Windows Terminal NIE jest zainstalowany
echo.
set /p wt="Zainstalowac Windows Terminal? (tak/nie): "
if /i "!wt!"=="tak" (
    start ms-windows-store://pdp/?ProductId=9N0DX20HK701
    pause
)

:setup_complete
echo.
echo ========================================
echo INSTALACJA ZAKONCZONA!
echo ========================================
echo.
echo Co dalej:
echo 1. Zamknij to okno
echo 2. Otworz nowy terminal
echo 3. Przejdz do folderu projektu
echo 4. Uruchom: Gemini_CLI.bat
echo 5. Zaloguj sie (jesli CLI o to poprosi)
echo.
pause
exit /b

:launcher
cls
echo ========================================
echo Gemini - AI Coding Assistant
echo ========================================
echo.
echo Folder projektu: %~dp0
echo.
echo  1. Uruchom Gemini - standardowo
echo  2. Uruchom Gemini - autonomicznie (Tryb -y)
echo  3. Debug (Tryb -d)
echo  4. Status instalacji
echo  5. Zmien konto (Auth)
echo  6. Wyczysc cache npm
echo  7. Instalator
echo  0. Wyjscie
echo.
set /p choice="Wybierz opcje (0-7): "

cd /d "%~dp0"

if "%choice%"=="1" (
    echo.
    echo Uruchamiam Gemini...
    echo.
    echo ========================================
    echo Gemini - tryb standardowy
    echo ========================================
    echo.
    cd /d "%~dp0"
    cmd /k "gemini"
    goto :launcher
)

if "%choice%"=="2" (
    echo.
    echo ========================================
    echo TRYB AUTONOMICZNY (YOLO MODE)
    echo ========================================
    timeout /t 2 > nul
    cd /d "%~dp0"
    cmd /k "gemini -y"
    goto :launcher
)

if "%choice%"=="3" (
    echo.
    echo ========================================
    echo Gemini - debug
    echo ========================================
    echo.
    cd /d "%~dp0"
    cmd /k "gemini -d"
    goto :launcher
)

if "%choice%"=="4" (
    cls
    echo ========================================
    echo STATUS INSTALACJI
    echo ========================================
    echo.
    echo Node.js:
    node --version 2>nul || echo NIE dziala
    
    echo.
    echo npm:
    npm --version 2>nul || echo NIE dziala
    
    echo.
    echo Git:
    git --version 2>nul || echo NIE dziala
    
    echo.
    echo Bash:
    bash --version 2>nul | findstr "version" || echo NIE dziala
    
    echo.
    echo Gemini:
    gemini --version 2>nul || echo NIE dziala
    
    echo.
    pause
    goto :launcher
)

if "%choice%"=="5" (
    echo.
    gemini auth
    pause
    goto :launcher
)

if "%choice%"=="6" (
    echo.
    npm cache clean --force
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