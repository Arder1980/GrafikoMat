@echo off
echo ========================================
echo   Aktualizacja Claude Project
echo ========================================
echo.

cd /d "C:\Users\adaml\OneDrive\Dokumenty\GrafikoMat"
python -m claude-pyrojects.cli update

echo.
echo ========================================
echo   Projekt zaktualizowany!
echo ========================================
echo   Teraz wejdz do Claude Projects
echo   i otworz projekt "GrafikoMat"
echo ========================================
pause