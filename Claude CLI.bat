@echo off
chcp 65001 > nul
echo ========================================
echo Claude - AI Coding Assistant
echo ========================================
echo.
echo Folder projektu: %~dp0
echo.
echo [1] Uruchom Claude (standardowo - wymaga potwierdzenia zmian)
echo [2] Uruchom autonomicznie (UWAGA - bez potwierdzenia!)
echo [3] Uruchom z debugowaniem MCP
echo [4] Sprawdź wersję Claude
echo [5] Wyloguj się z Claude
echo [6] Wyczyść cache npm
echo.
set /p choice="Wybierz opcje (1-6): "

cd /d "%~dp0"

if "%choice%"=="1" (
    echo.
    echo Uruchamiam Claude - tryb standardowy...
    echo.
    powershell -NoExit -Command "Write-Host 'Claude gotowy. Wpisz polecenie po angielsku...' -ForegroundColor Green; Write-Host ''; claude"
)

if "%choice%"=="2" (
    echo.
    echo ========================================
    echo UWAGA: Tryb autonomiczny!
    echo Claude będzie zmieniał pliki BEZ pytania!
    echo ========================================
    set /p confirm="Czy na pewno? (T/N): "
    if /i "%confirm%"=="T" (
        powershell -NoExit -Command "Write-Host 'Claude - tryb autonomiczny (brak potwierdzen!)' -ForegroundColor Red; Write-Host ''; claude --dangerously-skip-permissions"
    ) else (
        echo Anulowano.
        pause
    )
)

if "%choice%"=="3" (
    echo.
    echo Uruchamiam Claude z debugowaniem MCP...
    echo.
    powershell -NoExit -Command "Write-Host 'Claude - debug MCP' -ForegroundColor Yellow; Write-Host ''; claude --mcp-debug"
)

if "%choice%"=="4" (
    echo.
    claude --version
    echo.
    pause
)

if "%choice%"=="5" (
    echo.
    echo Wylogowywanie z Claude...
    claude --logout
    echo.
    pause
)

if "%choice%"=="6" (
    echo.
    echo Czyszczenie cache npm...
    npm cache clean --force
    echo.
    echo Cache wyczyszczony!
    pause
)

if "%choice%"=="" (
    echo.
    echo Nie wybrano opcji. Zamykam...
    timeout /t 2 > nul
)