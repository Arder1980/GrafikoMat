@echo off
echo Script started at %DATE% %TIME% > script_start_log.txt
chcp 65001 > nul
setlocal enabledelayedexpansion

echo ========================================
echo Claude Code + Supabase - Launcher
echo ========================================
echo.
REM Sprawdz czy jestesmy w trybie setup lub konfiguracji
if "%1"=="--setup" goto :setup
if "%1"=="--force-setup" goto :setup
if "%1"=="--config-supabase" goto :config_supabase

REM Sprawdz czy Claude jest zainstalowany
where claude >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    set /p install="Czy chcesz uruchomic instalator? (tak/nie): "
    if /i "!install:~0,1!"=="t" goto :setup
    echo.
    echo Anulowano. Aby uruchomic setup pozniej, uzyj: %~nx0 --setup
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

REM Stworz folder na backupy jezeli nie istnieje
if not exist "backups\db" mkdir backups\db >nul 2>&1

REM Zaladuj konfiguracje Supabase (jezeli istnieje)
if exist "supabase_config.bat" (
    call supabase_config.bat
)

REM Wszystko OK, pokaz menu launcher
goto :launcher

:setup
cls
echo ========================================
echo Claude Code + Supabase - INSTALATOR
echo ========================================
echo.
echo Ten skrypt zainstaluje:
echo [1] Node.js (jesli brak)
echo [2] Git for Windows (jesli brak)
echo [3] Claude Code CLI
echo [4] Supabase CLI (lokalnie w projekcie)
echo [5] Windows Terminal (opcjonalnie)
echo.
echo UWAGA: Supabase bedzie zainstalowany lokalnie (npm install --save-dev)
echo        Uzyj 'npx supabase' zamiast 'supabase'
echo.
pause

echo.
echo ========================================
echo Krok 1/5: Sprawdzanie Node.js
echo ========================================
echo.
echo === CAPTURING DEBUGGING INFO to debug_output.txt ===
del debug_output.txt >nul 2>&1
echo DEBUG: The PATH variable is: >> debug_output.txt
echo %PATH% >> debug_output.txt
echo. >> debug_output.txt
echo DEBUG: Running 'where node' command... >> debug_output.txt
where node >> debug_output.txt 2>&1
echo DEBUG: 'where node' returned errorlevel: %errorlevel% >> debug_output.txt
echo.
echo === DEBUGGING INFO CAPTURED. Please check debug_output.txt ===
echo.
pause
where node >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Node.js jest zainstalowany
    node --version
) else (
    echo [!] Node.js NIE jest zainstalowany lub nie ma go w PATH.
    echo.
    
    REM Sprawdz typowe lokalizacje
    set "node_dir="
    if exist "%ProgramFiles%\nodejs\node.exe" set "node_dir=%ProgramFiles%\nodejs"
    if exist "%ProgramFiles(x86)%\nodejs\node.exe" set "node_dir=%ProgramFiles(x86)%\nodejs"

    if defined node_dir (
        echo Wyglada na to, ze Node.js jest zainstalowany w: !node_dir!
        echo ale nie jest dodany do systemowej zmiennej PATH.
        echo.
        echo Aby dodac do PATH, uruchom PowerShell jako ADMINISTRATOR i wpisz:
        echo.
        echo   [System.Environment]::SetEnvironmentVariable^('PATH', [System.Environment]::GetEnvironmentVariable^('PATH', 'Machine'^) ^+ ';!node_dir!', 'Machine'^)
        echo.
        echo Po wykonaniu komendy ZRESTARTUJ ten terminal i uruchom skrypt ponownie.
        echo.
        echo Alternatywnie, mozesz otworzyc strone pobierania i przeinstalowac Node.js,
        echo upewniajac sie, ze opcja 'Add to PATH' jest zaznaczona.
        echo.
        set /p choice_node="Czy chcesz teraz otworzyc strone pobierania Node.js? (t/n): "
        if /i "!choice_node!"=="t" (
            start https://nodejs.org/en/download/
        )
    ) else (
        echo Nie znaleziono Node.js w typowych lokalizacjach.
        echo.
        echo Pobieranie Node.js...
        echo Otwieram strone pobierania. Pobierz i zainstaluj wersje LTS.
        echo Upewnij sie, ze podczas instalacji zaznaczona jest opcja 'Add to PATH'.
        echo Po instalacji ZRESTARTUJ ten skrypt.
        start https://nodejs.org/en/download/
    )
    pause
    exit /b
)

echo.
echo ========================================
echo Krok 2/5: Sprawdzanie Git for Windows
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
echo Krok 3/5: Sprawdzanie Git Bash w PATH
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
        echo Nie znaleziono Git Bash.
        echo Upewnij sie ze Git jest poprawnie zainstalowany.
        pause
        exit /b
    )
)

echo.
echo ========================================
echo Krok 4/5: Instalacja Claude Code
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
goto :install_supabase_cli

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

:install_supabase_cli
echo.
echo ========================================
echo Krok 4.5/5: Instalacja Supabase CLI
echo ========================================

cd /d "%~dp0"

if exist "node_modules\.bin\supabase.cmd" (
    echo [OK] Supabase CLI jest zainstalowany (lokalnie w projekcie)
    echo.
    set /p update="Czy chcesz zaktualizowac do najnowszej wersji? (tak/nie): "
    if /i "!update!"=="tak" goto :do_install_supabase
    if /i "!update!"=="t" goto :do_install_supabase
) else (
    echo [!] Supabase CLI NIE jest zainstalowany
    goto :do_install_supabase
)
goto :post_setup

:do_install_supabase
echo.
echo Instalowanie/Aktualizowanie Supabase CLI...
echo UWAGA: Instalacja lokalna w folderze projektu (--save-dev)
echo.
call npm install supabase --save-dev
if %errorlevel% neq 0 (
    echo.
    echo BLAD podczas instalacji!
    echo Sprawdz czy masz uprawnienia
    pause
    exit /b
)
echo.
echo [OK] Supabase CLI zainstalowany pomyslnie!
echo UWAGA: Uzyj 'npx supabase' do uruchamiania komend

:post_setup
echo.
echo ========================================
echo Krok 5/5: Windows Terminal
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
echo 2. Otworz NOWY PowerShell/Terminal
echo 3. Uruchom ponownie %~nx0
echo 4. Skonfiguruj projekt Supabase: %~nx0 --config-supabase
echo 5. Przy pierwszym uruchomieniu Claude zaloguj sie przez przegladarke
echo.
echo UWAGA: Supabase CLI zainstalowany lokalnie - uzyj 'npx supabase'
echo.
pause
exit /b

:config_supabase
cls
echo ========================================
echo Konfiguracja projektu Supabase
echo ========================================
echo.
cd /d "%~dp0"

if not exist "node_modules\.bin\supabase.cmd" (
    echo [BLAD] Supabase CLI nie jest zainstalowane!
    echo Uruchom najpierw: %~nx0 --setup
    pause
    exit /b
)

echo Aby skonfigurowac polaczenie z Supabase, potrzebujesz:
echo.
echo 1. Project Reference ID
echo    Dashboard ^> Settings ^> General ^> Reference ID
echo.
echo 2. Database Password
echo    Dashboard ^> Settings ^> Database ^> Reset Password
echo.
echo 3. Connection String (Direct connection)
echo    Dashboard ^> Connect ^> Direct connection
echo    Format: postgresql://postgres.[REF]^:[PASSWORD]@db.[REF].supabase.co^:5432/postgres
echo.
echo UWAGA: Jezeli haslo ma znaki specjalne (=, @, %%, itd.), uzyj percent-encoding!
echo        @ = %%%%40, = = %%%%3D, %%%% = %%%%25
echo.
pause

echo.
set /p project_ref="Podaj Project Reference ID: "
echo.
set /p db_password="Podaj Database Password: "
echo.
if "!project_ref!"=="" (
    echo [BLAD] Project Reference nie moze byc pusty!
    pause
    exit /b
)

if "!db_password!"=="" (
    echo [BLAD] Password nie moze byc pusty!
    pause
    exit /b
)

REM Stworz plik konfiguracyjny
echo @echo off > supabase_config.bat
echo REM Konfiguracja Supabase - wygenerowana automatycznie >> supabase_config.bat
echo set SUPABASE_PROJECT_REF=!project_ref! >> supabase_config.bat
echo set SUPABASE_DB_PASSWORD=!db_password! >> supabase_config.bat
echo set SUPABASE_DB_URL=postgresql://postgres.!project_ref!:!db_password!@db.!project_ref!.supabase.co:5432/postgres >> supabase_config.bat

echo.
echo [OK] Konfiguracja zapisana do: supabase_config.bat
echo.
REM Linkowanie projektu (UZYWAMY NPX!)
echo Linkowanie projektu Supabase...
echo.

npx supabase link --project-ref "!project_ref!" --password "!db_password!"
if %errorlevel% equ 0 (
    echo.
    echo [OK] Projekt zlinkowany pomyslnie!
    echo.
    echo Tworzenie folderu na backupy...
    if not exist "backups\db" mkdir backups\db
    echo [OK] Folder backups\db utworzony
) else (
    echo.
    echo [BLAD] Nie udalo sie polaczyc z projektem!
    echo Sprawdz czy dane sa poprawne i sprobuj ponownie.
)

echo.
echo ========================================
echo Konfiguracja zakonczona!
echo ========================================
echo.
pause
goto :launcher

:launcher
cls
echo ========================================
echo Claude - AI Coding Assistant + Supabase
echo ========================================
echo.
echo Folder projektu: %~dp0
echo.

REM Sprawdz status Supabase
if exist "supabase_config.bat" (
    call supabase_config.bat
    echo Status Supabase: [SKONFIGUROWANE] Ref: !SUPABASE_PROJECT_REF!
) else (
    echo Status Supabase: [NIESKONFIGUROWANE] - Uzyj opcji 9
)
echo.
echo === CLAUDE ===
echo [1] Claude - standardowo (z potwierdzeniami)
echo [2] Claude - AUTONOMICZNIE + AUTO-BACKUP bazy
echo [3] Claude - z debugowaniem MCP
echo.
echo === SUPABASE - BAZA DANYCH ===
echo [4] Backup bazy danych (reczny)
echo [5] Status bazy danych (dump schema)
echo [6] Rollback do ostatniego backupu
echo [7] Lista backupow
echo.
echo === KONFIGURACJA ===
echo [8] Status instalacji (wersje)
echo [9] Konfiguruj projekt Supabase
echo [A] Wyloguj z Claude
echo [B] Wyczysc cache npm
echo [C] Uruchom ponownie instalator
echo [0] Wyjscie
echo.
set /p choice="Wybierz opcje: "

cd /d "%~dp0"

if "%choice%"=="1" (
    echo.
    echo Uruchamiam Claude...
    echo.
    powershell -NoExit -Command "Write-Host 'Claude gotowy' -ForegroundColor Green; claude"
    exit /b
)

if "%choice%"=="2" (
    if not exist "supabase_config.bat" (
        echo.
        echo [BLAD] Supabase nie jest skonfigurowane!
        echo Najpierw uzyj opcji 9.
        pause
        goto :launcher
    )
    
    echo.
    echo === TRYB AUTONOMICZNY + AUTO-BACKUP ===
    echo.
    echo UWAGA: 
    echo - Pliki beda zmieniane automatycznie BEZ potwierdzen
    echo - Baza danych BEDZIE backupowana przed pierwsza zmiana
    echo.
    timeout /t 3
    
    call supabase_config.bat
    
    echo Tworzenie backupu bazy danych...
    set timestamp=%date:~-4,4%%date:~-7,2%%date:~-10,2%_%time:~0,2%%time:~3,2%%time:~6,2%
    set timestamp=%timestamp: =0%
    
    npx supabase db dump --db-url "%SUPABASE_DB_URL%" -f backups\db\auto_before_session_%timestamp%.sql
    
    if %errorlevel% equ 0 (
        echo [OK] Backup: backups\db\auto_before_session_%timestamp%.sql
        echo.
        echo Uruchamiam Claude w trybie autonomicznym...
        powershell -NoExit -Command "Write-Host 'Claude - tryb autonomiczny + auto-backup' -ForegroundColor Red; claude --dangerously-skip-permissions"
    ) else (
        echo [BLAD] Nie udalo sie zrobic backupu!
        echo Anulowano uruchomienie w trybie autonomicznym.
        pause
        goto :launcher
    )
    exit /b
)

if "%choice%"=="3" (
    echo.
    powershell -NoExit -Command "Write-Host 'Claude - debug MCP' -ForegroundColor Yellow; claude --mcp-debug"
    exit /b
)

if "%choice%"=="4" (
    if not exist "supabase_config.bat" (
        echo.
        echo [BLAD] Supabase nie jest skonfigurowane!
        echo Najpierw uzyj opcji 9.
        pause
        goto :launcher
    )
    
    echo.
    echo === Backup bazy danych ===
    echo.
    
    call supabase_config.bat
    
    set timestamp=%date:~-4,4%%date:~-7,2%%date:~-10,2%_%time:~0,2%%time:~3,2%%time:~6,2%
    set timestamp=%timestamp: =0%
    
    echo Tworzenie backupu...
    npx supabase db dump --db-url "%SUPABASE_DB_URL%" -f backups\db\manual_backup_%timestamp%.sql
    
    if %errorlevel% equ 0 (
        echo.
        echo [OK] Backup zapisany: backups\db\manual_backup_%timestamp%.sql
    ) else (
        echo.
        echo [BLAD] Nie udalo sie zrobic backupu!
    )
    echo.
    pause
    goto :launcher
)

if "%choice%"=="5" (
    if not exist "supabase_config.bat" (
        echo.
        echo [BLAD] Supabase nie jest skonfigurowane!
        echo Najpierw uzyj opcji 9.
        pause
        goto :launcher
    )
    
    echo.
    echo === Status bazy danych ===
    echo.
    
    call supabase_config.bat
    
    echo Pobieranie schematu bazy...
    npx supabase db dump --db-url "%SUPABASE_DB_URL%" --schema-only -f temp_schema_preview.sql
    
    if %errorlevel% equ 0 (
        echo.
        echo [OK] Schemat pobrany - otwieranie w notatniku...
        notepad temp_schema_preview.sql
        del temp_schema_preview.sql
    ) else (
        echo.
        echo [BLAD] Nie udalo sie pobrac schematu!
    )
    echo.
    pause
    goto :launcher
)

if "%choice%"=="6" goto :do_rollback

if "%choice%"=="7" (
    echo.
    echo === Lista backupow ===
    echo.
    
    if not exist "backups\db\*.sql" (
        echo Brak backupow w folderze backups\db\
    ) else (
        dir /b /o-d backups\db\*.sql
    )
    
    echo.
    pause
    goto :launcher
)

if "%choice%"=="8" (
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
    echo Supabase CLI (lokalny):
    if exist "node_modules\.bin\supabase.cmd" (
        npx supabase --version
    ) else (
        echo [!] Nie zainstalowane
    )
    echo.
    echo Windows Terminal:
    where wt.exe >nul 2>&1 && echo [OK] Zainstalowane || echo [!] Nie zainstalowane
    echo.
    echo Projekt Supabase:
    if exist "supabase_config.bat" (
        call supabase_config.bat
        echo [OK] Skonfigurowane - Ref: !SUPABASE_PROJECT_REF!
    ) else (
        echo [!] Nieskonfigurowane
    )
    echo.
    pause
    goto :launcher
)

if "%choice%"=="9" (
    goto :config_supabase
)

if /i "%choice%"=="a" (
    echo.
    echo Wylogowywanie...
    claude --logout
    echo.
    pause
    goto :launcher
)

if /i "%choice%"=="b" (
    echo.
    echo Czyszczenie cache...
    npm cache clean --force
    echo [OK] Cache wyczyszczony
    echo.
    pause
    goto :launcher
)

if /i "%choice%"=="c" (
    goto :setup
)

if "%choice%"=="0" (
    exit /b
)

echo.
echo Nieprawidlowa opcja!
timeout /t 2 > nul
goto :launcher


REM ========================================
REM SUBPROGRAM: Rollback
REM ========================================

:do_rollback
if not exist "supabase_config.bat" (
    echo.
    echo [BLAD] Supabase nie jest skonfigurowane!
    pause
    goto :launcher
)

if not exist "backups\db\*.sql" (
    echo.
    echo [BLAD] Brak backupow w folderze backups\db\
    pause
    goto :launcher
)

echo.
echo === Rollback do ostatniego backupu ===
echo.
echo UWAGA: To nadpisze aktualna baze danych!
echo.
REM Znajdz ostatni backup
for /f "delims=" %%i in ('dir /b /o-d backups\db\*.sql 2^>nul') do (
    set last_backup=%%i
    goto :found_backup
)

:found_backup
if not defined last_backup (
    echo [BLAD] Nie znaleziono backupow!
    pause
    goto :launcher
)

echo Ostatni backup: !last_backup!
echo.
set /p confirm="Czy na pewno chcesz przywrocic ten backup? (tak/nie): "

if /i not "!confirm!"=="tak" (
    echo Anulowano.
    pause
    goto :launcher
)

call supabase_config.bat

echo.
echo Przywracanie backupu...
echo UWAGA: To moze potrwac kilka minut...
echo.
REM Najpierw drop wszystkich obiektow
psql "%SUPABASE_DB_URL%" -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"

REM Przywroc backup
psql "%SUPABASE_DB_URL%" < backups\db\!last_backup!

if %errorlevel% equ 0 (
    echo.
    echo [OK] Backup przywrocony pomyslnie!
) else (
    echo.
    echo [BLAD] Wystapil problem podczas przywracania!
)
echo.
pause
goto :launcher