# Debugowanie zapisu deklaracji do Supabase

## ✅ NAPRAWIONO (najnowsza wersja):

### Problem: Błąd "cannot insert a non-DEFAULT value into column id"
**Objawy:**
- Komunikat: `Column "id" is an identity column defined as GENERATED ALWAYS`
- Hint: `Use OVERRIDING SYSTEM VALUE to override`
- Dane nie trafiają do bazy

**Rozwiązanie:**
- Zmieniono atrybut `[PrimaryKey("id", false)]` na `[PrimaryKey("id", shouldInsert: false)]`
- Teraz pole `id` NIE jest wysyłane przy INSERT (Postgres je generuje automatycznie)

## ✅ NAPRAWIONO (poprzednie wersje):

### Problem: ViewModel był dispose'owany przed zakończeniem zapisu
**Objawy:**
- Komunikat "DeclarationsViewModel Disposed for 2025-10" w środku zapisu
- `SelectedDoctor.get returned null`
- Dane nie trafiały do bazy

**Rozwiązanie:**
- Zmieniono `DoSave()` z `async void` na `async Task DoSaveAsync()`
- Dodano `AsyncRelayCommand SaveAsyncCommand`
- Przycisk "Zapisz i zamknij" teraz czeka (`await`) na zakończenie zapisu przed dispose

### Problem: Konflikt nazw JSON
**Rozwiązano:** Zmieniono `Day` (numer dnia) na `dayNumber` w JSON

## Zmiany wprowadzone:

### 1. Wyświetlanie pełnych nazw w menu
- ✅ Menu kontekstowe teraz pokazuje: "Mogę (MOG)", "Chcę (CHC)" itp.
- Skróty (MOG, CHC, WAR) są zapisywane w bazie danych
- Pełne nazwy są widoczne dla użytkownika

### 2. Dodane logi debugowania
W `DeclarationsViewModel.DoSave()` dodano szczegółowe logi:
- `[SAVE] DoSave called` - metoda została wywołana
- `[SAVE] _declarationRepository=...` - sprawdzenie czy repozytorium istnieje
- `[SAVE] Attempting to save to Supabase...` - rozpoczęcie zapisu
- `[SAVE] Save to Supabase completed successfully` - sukces
- `[SAVE] ERROR during save...` - błąd z pełnym stack trace

## Jak debugować:

### Krok 1: Sprawdź logi w Visual Studio Output
1. Uruchom aplikację w Visual Studio (F5)
2. Otwórz widok deklaracji
3. Dodaj deklarację (prawy klik → wybierz "Chcę (CHC)")
4. Kliknij przycisk "Zapisz"
5. Sprawdź okno **Output** (View → Output lub Ctrl+Alt+O)
6. Szukaj linii zaczynających się od `[SAVE]`

### Krok 2: Sprawdź czy repozytorium jest dostępne
Jeśli zobaczysz:
```
[SAVE] Skipping Supabase save - repository or unit not available
```

Oznacza to że:
- `_declarationRepository` jest null (nie udało się utworzyć repozytorium)
- `_currentUnitId` jest null
- `SelectedDoctor` jest null

**Rozwiązanie:**
- Sprawdź czy w `MainWindow.xaml.cs` linia 637 utworzyła repozytorium
- Sprawdź czy w `MainWindow.xaml.cs` linia 963-964 przekazano repozytorium i unitId

### Krok 3: Sprawdź błędy Supabase
Jeśli zobaczysz:
```
[SAVE] ERROR during save to Supabase: ...
```

Możliwe przyczyny:
1. **Brak połączenia z Supabase** - sprawdź ustawienia połączenia
2. **Nie wykonano skryptu SQL** - wykonaj `database/declarations_setup.sql`
3. **Błąd RLS (Row Level Security)** - sprawdź polityki w Supabase
4. **Błąd serializacji JSON** - sprawdź strukturę danych

### Krok 4: Sprawdź czy dane trafiły do bazy
1. Otwórz Supabase Dashboard
2. Przejdź do Table Editor → declarations
3. Sprawdź czy jest wpis dla twojego lekarza/jednostki/miesiąca

```sql
SELECT * FROM declarations
WHERE year = 2025
  AND month = 1
ORDER BY last_modified DESC;
```

### Krok 5: Sprawdź format JSON w bazie
Dane powinny wyglądać tak:
```json
{
  "days": [
    {
      "dayNumber": 1,
      "mode": "Full24",
      "full": "CHC"
    },
    {
      "dayNumber": 2,
      "mode": "Split12",
      "day": "MOG",
      "night": "---"
    }
  ]
}
```

**UWAGA:** `dayNumber` to numer dnia w miesiącu (1-31), a `day` to deklaracja dla slotu dziennego (7:00-19:00)

## Najczęstsze problemy:

### Problem 1: Repozytorium jest null
**Przyczyna:** `_supabaseService.Client` jest null
**Rozwiązanie:**
- Sprawdź ustawienia połączenia w aplikacji
- Upewnij się że Supabase URL i Anon Key są poprawne

### Problem 2: "Permission denied"
**Przyczyna:** Polityki RLS nie pozwalają na zapis
**Rozwiązanie:**
- Wykonaj skrypt SQL: `database/declarations_setup.sql`
- Sprawdź czy użytkownik jest zalogowany
- Sprawdź czy użytkownik należy do jednostki

### Problem 3: "relation declarations does not exist"
**Przyczyna:** Tabela nie istnieje lub ma złą nazwę
**Rozwiązanie:**
- Sprawdź czy tabela nazywa się dokładnie `declarations` (małe litery)
- Sprawdź czy schemat to `public`

### Problem 4: Dane zapisują się ale znikają po restarcie
**Przyczyna:**
- Zapis do pamięci działa, ale nie do Supabase
- Sprawdź logi czy pojawia się komunikat "Save to Supabase completed successfully"

**Rozwiązanie:**
- Zobacz logi debugowania (Krok 1)
- Sprawdź czy nie ma błędów (Krok 3)

## Kontakt
Jeśli nadal masz problemy, wklej logi z Output window zaczynające się od `[SAVE]`
