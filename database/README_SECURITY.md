# 🔒 Bezpieczeństwo danych wrażliwych w GrafikoMat

## Problem #003: Database password w plain text

### Obecna sytuacja
Plik `supabase_config.bat` zawiera hasło do bazy danych PostgreSQL w postaci zwykłego tekstu (plain text).

### Rozwiązanie zaimplementowane

#### 1. **Ochrona przed commitowaniem do repo**
✅ `supabase_config.bat` jest w `.gitignore` (linia 371)
- Plik **NIE TRAFI** do repozytorium Git
- Nie będzie widoczny publicznie

#### 2. **Zalecane dodatkowe działania** (opcjonalne, dla użytkownika)

**Opcja A: Przenieś plik poza folder projektu**
```batch
REM Przenieś supabase_config.bat do katalogu użytkownika
move supabase_config.bat "%USERPROFILE%\supabase_config.bat"

REM Zaktualizuj "Claude CLI + Supabase.bat" (linie 43-45):
REM Zmień:
if exist "supabase_config.bat" (
    call supabase_config.bat
)

REM Na:
if exist "%USERPROFILE%\supabase_config.bat" (
    call "%USERPROFILE%\supabase_config.bat"
)
```

**Opcja B: Użyj Windows Credential Manager (manualnie)**
```batch
REM Zapisz hasło do Credential Manager:
cmdkey /generic:"GrafikoMat_Supabase" /user:"postgres" /pass:"TWOJE_HASLO"

REM Zweryfikuj:
cmdkey /list | findstr "GrafikoMat"

REM W supabase_config.bat zamień hasło na odczyt z Credential Manager:
REM UWAGA: cmdkey nie pozwala na bezpośredni odczyt hasła (security by design)
REM Trzeba użyć PowerShell lub innych narzędzi
```

**Opcja C: Zmienne środowiskowe Windows (najlepsze dla production)**
1. Otwórz: Panel sterowania → System → Zaawansowane ustawienia systemu → Zmienne środowiskowe
2. Dodaj nową zmienną użytkownika:
   - Nazwa: `SUPABASE_DB_PASSWORD`
   - Wartość: twoje hasło
3. Usuń `supabase_config.bat`
4. Skrypty będą automatycznie używać zmiennej środowiskowej

#### 3. **Dlaczego nie użyto Windows Credential Manager automatycznie?**

Windows Credential Manager (`cmdkey`) **nie pozwala na bezpośredni odczyt hasła** z wiersza poleceń z powodów bezpieczeństwa. Hasła mogą być odczytane tylko przez:
- Aplikacje używające Windows Credential API (CredRead)
- PowerShell z modułem `CredentialManager` (wymaga instalacji)
- Interaktywne okno dialogowe (Get-Credential)

Dla prostoty użycia (brak dodatkowych zależności), obecne rozwiązanie używa pliku lokalnego z następującymi zabezpieczeniami:
1. ✅ Plik w `.gitignore` - nie trafi do repo
2. ✅ Komentarz ostrzegawczy w pliku
3. ✅ Instrukcje przeniesienia pliku poza projekt (ten dokument)

### Weryfikacja bezpieczeństwa

```bash
# Sprawdź czy supabase_config.bat jest w .gitignore:
git check-ignore supabase_config.bat
# Powinno zwrócić: supabase_config.bat

# Sprawdź czy plik nie jest w repo:
git ls-files | grep supabase_config
# Powinno być PUSTE

# Sprawdź historię Git:
git log --all --full-history -- supabase_config.bat
# Powinno być PUSTE (jeśli nigdy nie był commitowany)
```

### Status naprawy
- ✅ Plik dodany do `.gitignore`
- ✅ Komentarze ostrzegawcze w skrypcie
- ✅ Dokumentacja bezpieczeństwa (ten plik)
- ⚠️ Hasło nadal w plain text lokalnie (do ręcznej poprawy przez użytkownika)

### Rekomendacje dla użytkownika

**Priorytet WYSOKI:**
1. Po skonfigurowaniu projektu, przenieś `supabase_config.bat` poza folder projektu
2. Ustaw uprawnienia pliku: tylko Twój użytkownik Windows może czytać

**Priorytet ŚREDNI:**
3. Regularnie zmieniaj hasło do bazy danych (Supabase Dashboard → Database → Reset Password)
4. Użyj zmiennych środowiskowych Windows dla production deployment

**Priorytet NISKI:**
5. Rozważ instalację modułu PowerShell `CredentialManager` dla automatyzacji

---

**Data utworzenia:** 2025-11-14
**Problem ID:** #003
**Status:** ✅ Częściowo naprawiony (wymaga akcji użytkownika dla pełnego zabezpieczenia)
