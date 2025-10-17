# ===== KONFIGURACJA =====
# Wklej tutaj ID Gista
$GIST_ID = "bf8c2b034daec5a32bd3c588055d83a0"

# Sciezka do Twojego snapshotu
$SNAPSHOT_PATH = "C:\Users\adaml\OneDrive\Dokumenty\GrafikoMat\ProjektSnapshot_utf8.txt"

# ===== AKTUALIZACJA =====
Write-Host "Updating Gist..." -ForegroundColor Yellow

gh gist edit $GIST_ID --add $SNAPSHOT_PATH

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Gist updated!" -ForegroundColor Green
} else {
    Write-Host "ERROR: Update failed!" -ForegroundColor Red
}