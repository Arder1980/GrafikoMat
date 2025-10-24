-- ============================================================================
-- Skrypt SQL dla tabeli declarations w Supabase
-- ============================================================================
-- UWAGA: Wykonaj ten skrypt w Supabase SQL Editor
-- ============================================================================

-- 1. Dodanie constraint UNIQUE dla zapewnienia jednej deklaracji na lekarza/miesiąc/jednostkę
-- ============================================================================
ALTER TABLE public.declarations
DROP CONSTRAINT IF EXISTS unique_declaration_per_doctor_month;

ALTER TABLE public.declarations
ADD CONSTRAINT unique_declaration_per_doctor_month
UNIQUE (unit_id, doctor_id, year, month);

-- 2. Dodanie indeksów dla szybszych zapytań
-- ============================================================================

-- Indeks dla zapytań pobierających wszystkie deklaracje jednostki w danym miesiącu
CREATE INDEX IF NOT EXISTS idx_declarations_unit_year_month
ON public.declarations(unit_id, year, month);

-- Indeks dla zapytań pobierających deklarację konkretnego lekarza
CREATE INDEX IF NOT EXISTS idx_declarations_doctor_year_month
ON public.declarations(doctor_id, year, month);

-- Indeks dla zapytań wykorzystujących last_modified (np. sortowanie, filtrowanie)
CREATE INDEX IF NOT EXISTS idx_declarations_last_modified
ON public.declarations(last_modified DESC);

-- 3. Włączenie Row Level Security (RLS)
-- ============================================================================
ALTER TABLE public.declarations ENABLE ROW LEVEL SECURITY;

-- 4. Usunięcie istniejących polityk (jeśli istnieją)
-- ============================================================================
DROP POLICY IF EXISTS "Users can view declarations from their units" ON public.declarations;
DROP POLICY IF EXISTS "Users can edit their own declarations" ON public.declarations;
DROP POLICY IF EXISTS "Admins can edit declarations in their units" ON public.declarations;
DROP POLICY IF EXISTS "SuperAdmins can edit all declarations" ON public.declarations;
DROP POLICY IF EXISTS "Users can insert their own declarations" ON public.declarations;
DROP POLICY IF EXISTS "Users can delete their own declarations" ON public.declarations;
DROP POLICY IF EXISTS "Admins can delete declarations in their units" ON public.declarations;

-- 5. Polityki RLS - SELECT (odczyt)
-- ============================================================================

-- Wszyscy użytkownicy mogą przeglądać deklaracje ze swoich jednostek
CREATE POLICY "Users can view declarations from their units"
ON public.declarations
FOR SELECT
USING (
  unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
);

-- 6. Polityki RLS - INSERT (wstawianie)
-- ============================================================================

-- Użytkownicy mogą dodawać tylko swoje własne deklaracje
CREATE POLICY "Users can insert their own declarations"
ON public.declarations
FOR INSERT
WITH CHECK (
  doctor_id = auth.uid()
  AND unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
);

-- 7. Polityki RLS - UPDATE (aktualizacja)
-- ============================================================================

-- Użytkownicy mogą edytować tylko swoje deklaracje
CREATE POLICY "Users can edit their own declarations"
ON public.declarations
FOR UPDATE
USING (doctor_id = auth.uid())
WITH CHECK (doctor_id = auth.uid());

-- Admini (admin_level >= 1) mogą edytować wszystkie deklaracje w swoich jednostkach
CREATE POLICY "Admins can edit declarations in their units"
ON public.declarations
FOR UPDATE
USING (
  EXISTS (
    SELECT 1
    FROM public.doctors
    WHERE id = auth.uid() AND admin_level >= 1
  )
  AND unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
)
WITH CHECK (
  EXISTS (
    SELECT 1
    FROM public.doctors
    WHERE id = auth.uid() AND admin_level >= 1
  )
  AND unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
);

-- SuperAdmini (admin_level >= 9) mogą edytować wszystkie deklaracje
CREATE POLICY "SuperAdmins can edit all declarations"
ON public.declarations
FOR UPDATE
USING (
  EXISTS (
    SELECT 1
    FROM public.doctors
    WHERE id = auth.uid() AND admin_level >= 9
  )
)
WITH CHECK (
  EXISTS (
    SELECT 1
    FROM public.doctors
    WHERE id = auth.uid() AND admin_level >= 9
  )
);

-- 8. Polityki RLS - DELETE (usuwanie)
-- ============================================================================

-- Użytkownicy mogą usuwać tylko swoje deklaracje
CREATE POLICY "Users can delete their own declarations"
ON public.declarations
FOR DELETE
USING (doctor_id = auth.uid());

-- Admini mogą usuwać deklaracje w swoich jednostkach
CREATE POLICY "Admins can delete declarations in their units"
ON public.declarations
FOR DELETE
USING (
  EXISTS (
    SELECT 1
    FROM public.doctors
    WHERE id = auth.uid() AND admin_level >= 1
  )
  AND unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
);

-- ============================================================================
-- Koniec skryptu
-- ============================================================================

-- Weryfikacja utworzonych indeksów:
-- SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'declarations';

-- Weryfikacja polityk RLS:
-- SELECT policyname, cmd, qual, with_check FROM pg_policies WHERE tablename = 'declarations';
