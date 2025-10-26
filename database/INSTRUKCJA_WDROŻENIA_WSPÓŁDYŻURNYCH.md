# Instrukcja wdrożenia funkcjonalności współdyżurnych

## Problem: Row-Level Security Policy Error

Jeśli otrzymujesz błąd: **"row violates row-level security policy for table co_duty_notifications"**, oznacza to, że polityki RLS nie zostały jeszcze utworzone w bazie danych Supabase.

## Rozwiązanie: Wykonaj skrypt SQL w Supabase

### Krok 1: Otwórz Supabase SQL Editor

1. Zaloguj się do [Supabase Dashboard](https://app.supabase.com)
2. Wybierz swój projekt
3. Z menu po lewej stronie wybierz **SQL Editor**

### Krok 2: Wykonaj skrypt co_duty_setup.sql

1. Otwórz plik `database/co_duty_setup.sql` w edytorze tekstu
2. Skopiuj **całą zawartość** skryptu
3. Wklej do SQL Editor w Supabase
4. Kliknij **Run** (lub Ctrl+Enter)

### Krok 3: Weryfikacja

Po wykonaniu skryptu możesz zweryfikować czy wszystko się udało:

```sql
-- Sprawdź czy tabela została utworzona
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'co_duty_notifications'
ORDER BY ordinal_position;

-- Sprawdź polityki RLS
SELECT policyname, cmd, qual, with_check
FROM pg_policies
WHERE tablename = 'co_duty_notifications';
```

Powinny pojawić się 4 polityki:
- `Users see their own notifications` (SELECT)
- `Users can create notifications` (INSERT)
- `Recipients can update notification status` (UPDATE)
- `Both parties can delete notifications` (DELETE)

## Co robi skrypt?

Skrypt `co_duty_setup.sql` wykonuje następujące operacje:

1. **Rozszerza tabelę `declarations`** o pola współdyżurnych:
   - `co_duty_partner_id` - UUID partnera
   - `co_duty_status` - status ('pending', 'accepted', 'rejected')
   - `co_duty_initiator_id` - UUID inicjatora

2. **Tworzy tabelę `co_duty_notifications`** dla powiadomień o prośbach

3. **Tworzy indeksy** dla szybszych zapytań

4. **Konfiguruje polityki RLS** (Row Level Security):
   - Użytkownicy widzą tylko swoje powiadomienia (jako nadawca lub odbiorca)
   - Tylko nadawca może utworzyć powiadomienie
   - Tylko odbiorca może zaktualizować status
   - Obie strony mogą usuwać powiadomienia

5. **Aktualizuje polityki RLS dla `declarations`**:
   - Pozwala na tworzenie deklaracji dla partnera (jeśli jesteś inicjatorem)
   - Pozwala na edycję deklaracji gdzie jesteś partnerem
   - Pozwala na usuwanie deklaracji gdzie jesteś partnerem

## Uwaga dotycząca auth.uid()

Polityki RLS zakładają że:
- `doctors.id` = UUID użytkownika z Supabase Auth (`auth.uid()`)
- Każdy lekarz w tabeli `doctors` ma taki sam UUID jak jego konto w `auth.users`

Jeśli to nie jest spełnione, polityki RLS nie będą działać poprawnie i będziesz otrzymywać błędy dostępu.

## Co dalej?

Po wykonaniu skryptu:
1. Zrestartuj aplikację GrafikoMat
2. Przetestuj dodawanie współdyżurnego - błąd RLS powinien zniknąć
3. Menu dzwonka będzie pokazywać "Brak nowych powiadomień" gdy nie ma powiadomień
