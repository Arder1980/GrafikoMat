-- ============================================================================
-- Czyszczenie testowych danych z tabeli declarations
-- ============================================================================
-- UWAGA: To usunie WSZYSTKIE deklaracje z bazy!
-- Używaj tylko na etapie developmentu/testowania
-- ============================================================================

-- 1. Usuń wszystkie deklaracje
DELETE FROM declarations;

-- 2. Zresetuj sekwencję ID do 1
SELECT setval('declarations_id_seq', 1, false);

-- 3. Weryfikacja - tabela powinna być pusta
SELECT COUNT(*) as total_declarations FROM declarations;

-- 4. Sprawdź sekwencję - powinna być 1
SELECT last_value FROM declarations_id_seq;
