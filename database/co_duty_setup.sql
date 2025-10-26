-- ============================================================================
-- Skrypt SQL dla funkcjonalności współdyżurnych w Supabase
-- ============================================================================
-- UWAGA: Wykonaj ten skrypt w Supabase SQL Editor
-- ============================================================================

-- 1. Rozszerzenie tabeli declarations o pola współdyżurnych
-- ============================================================================

ALTER TABLE public.declarations
ADD COLUMN IF NOT EXISTS co_duty_partner_id UUID REFERENCES public.doctors(id),
ADD COLUMN IF NOT EXISTS co_duty_status TEXT CHECK (co_duty_status IN ('pending', 'accepted', 'rejected')),
ADD COLUMN IF NOT EXISTS co_duty_initiator_id UUID REFERENCES public.doctors(id);

-- 2. Indeksy dla szybszych zapytań
-- ============================================================================

-- Indeks dla zapytań wyszukujących pary współdyżurnych
CREATE INDEX IF NOT EXISTS idx_declarations_co_duty_partner
ON public.declarations(co_duty_partner_id)
WHERE co_duty_partner_id IS NOT NULL;

-- Indeks dla zapytań filtrujących po statusie współdyżuru
CREATE INDEX IF NOT EXISTS idx_declarations_co_duty_status
ON public.declarations(co_duty_status)
WHERE co_duty_status IS NOT NULL;

-- Indeks dla zapytań wyszukujących inicjatora
CREATE INDEX IF NOT EXISTS idx_declarations_co_duty_initiator
ON public.declarations(co_duty_initiator_id)
WHERE co_duty_initiator_id IS NOT NULL;

-- 3. Tabela powiadomień o współdyżurach
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.co_duty_notifications (
  id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
  from_doctor_id UUID NOT NULL REFERENCES public.doctors(id) ON DELETE CASCADE,
  to_doctor_id UUID NOT NULL REFERENCES public.doctors(id) ON DELETE CASCADE,
  unit_id UUID NOT NULL REFERENCES public.units(id) ON DELETE CASCADE,
  year INT NOT NULL,
  month INT NOT NULL,
  day INT NOT NULL,
  slot_part TEXT NOT NULL CHECK (slot_part IN ('full', 'day', 'night')),
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'rejected')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  responded_at TIMESTAMPTZ,

  -- Constraint zapewniający unikalność powiadomienia
  CONSTRAINT unique_notification UNIQUE (from_doctor_id, to_doctor_id, unit_id, year, month, day, slot_part)
);

-- 4. Indeksy dla tabeli co_duty_notifications
-- ============================================================================

-- Indeks dla zapytań pobierających oczekujące powiadomienia dla użytkownika
CREATE INDEX IF NOT EXISTS idx_notifications_to_doctor_pending
ON public.co_duty_notifications(to_doctor_id, status)
WHERE status = 'pending';

-- Indeks dla zapytań wyszukujących powiadomienia od konkretnego lekarza
CREATE INDEX IF NOT EXISTS idx_notifications_from_doctor
ON public.co_duty_notifications(from_doctor_id);

-- Indeks dla zapytań wyszukujących powiadomienia dla jednostki/daty
CREATE INDEX IF NOT EXISTS idx_notifications_unit_date
ON public.co_duty_notifications(unit_id, year, month, day);

-- Indeks dla sortowania po dacie utworzenia
CREATE INDEX IF NOT EXISTS idx_notifications_created_at
ON public.co_duty_notifications(created_at DESC);

-- 5. Włączenie Row Level Security dla co_duty_notifications
-- ============================================================================

ALTER TABLE public.co_duty_notifications ENABLE ROW LEVEL SECURITY;

-- 6. Usunięcie istniejących polityk (jeśli istnieją)
-- ============================================================================

DROP POLICY IF EXISTS "Users see their own notifications" ON public.co_duty_notifications;
DROP POLICY IF EXISTS "Users can create notifications" ON public.co_duty_notifications;
DROP POLICY IF EXISTS "Recipients can update notification status" ON public.co_duty_notifications;
DROP POLICY IF EXISTS "Both parties can delete notifications" ON public.co_duty_notifications;

-- 7. Polityki RLS dla co_duty_notifications - SELECT
-- ============================================================================

-- Użytkownicy widzą tylko swoje powiadomienia (jako nadawca lub odbiorca)
CREATE POLICY "Users see their own notifications"
ON public.co_duty_notifications
FOR SELECT
USING (
  auth.uid() = from_doctor_id OR auth.uid() = to_doctor_id
);

-- 8. Polityki RLS dla co_duty_notifications - INSERT
-- ============================================================================

-- Tylko nadawca może utworzyć powiadomienie (doctor_id musi być równy from_doctor_id)
CREATE POLICY "Users can create notifications"
ON public.co_duty_notifications
FOR INSERT
WITH CHECK (
  auth.uid() = from_doctor_id
  AND unit_id IN (
    SELECT unit_id
    FROM public.unit_doctors
    WHERE doctor_id = auth.uid() AND is_active = true
  )
);

-- 9. Polityki RLS dla co_duty_notifications - UPDATE
-- ============================================================================

-- Tylko odbiorca może zaktualizować status powiadomienia
CREATE POLICY "Recipients can update notification status"
ON public.co_duty_notifications
FOR UPDATE
USING (auth.uid() = to_doctor_id)
WITH CHECK (auth.uid() = to_doctor_id);

-- 10. Polityki RLS dla co_duty_notifications - DELETE
-- ============================================================================

-- Obie strony mogą usuwać powiadomienia
CREATE POLICY "Both parties can delete notifications"
ON public.co_duty_notifications
FOR DELETE
USING (
  auth.uid() = from_doctor_id OR auth.uid() = to_doctor_id
);

-- 11. Rozszerzenie polityk RLS dla declarations o współdyżurnych
-- ============================================================================

-- UWAGA: Polityka INSERT musi pozwolić na tworzenie deklaracji dla partnera
-- Usuwamy starą politykę i tworzymy nową

DROP POLICY IF EXISTS "Users can insert their own declarations" ON public.declarations;

-- Użytkownicy mogą dodawać swoje własne deklaracje LUB deklaracje partnera (jeśli są inicjatorem)
CREATE POLICY "Users can insert their own declarations"
ON public.declarations
FOR INSERT
WITH CHECK (
  (
    -- Własna deklaracja
    doctor_id = auth.uid()
    AND unit_id IN (
      SELECT unit_id
      FROM public.unit_doctors
      WHERE doctor_id = auth.uid() AND is_active = true
    )
  )
  OR
  (
    -- Deklaracja partnera (gdy jestem inicjatorem)
    co_duty_initiator_id = auth.uid()
    AND unit_id IN (
      SELECT unit_id
      FROM public.unit_doctors
      WHERE doctor_id = auth.uid() AND is_active = true
    )
    AND doctor_id IN (
      SELECT id FROM public.doctors WHERE is_archived = false
    )
  )
);

-- UWAGA: Polityka UPDATE musi pozwolić na aktualizację deklaracji partnera
-- Usuwamy starą politykę i tworzymy nową

DROP POLICY IF EXISTS "Users can edit their own declarations" ON public.declarations;

-- Użytkownicy mogą edytować swoje deklaracje LUB deklaracje gdzie są partnerem
CREATE POLICY "Users can edit their own declarations"
ON public.declarations
FOR UPDATE
USING (
  doctor_id = auth.uid()
  OR co_duty_partner_id = auth.uid()
)
WITH CHECK (
  doctor_id = auth.uid()
  OR co_duty_partner_id = auth.uid()
);

-- UWAGA: Polityka DELETE musi pozwolić na usuwanie deklaracji partnera
-- Usuwamy starą politykę i tworzymy nową

DROP POLICY IF EXISTS "Users can delete their own declarations" ON public.declarations;

-- Użytkownicy mogą usuwać swoje deklaracje LUB deklaracje gdzie są partnerem
CREATE POLICY "Users can delete their own declarations"
ON public.declarations
FOR DELETE
USING (
  doctor_id = auth.uid()
  OR co_duty_partner_id = auth.uid()
);

-- ============================================================================
-- Koniec skryptu
-- ============================================================================

-- Weryfikacja nowych kolumn w declarations:
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'declarations' AND column_name LIKE 'co_duty%';

-- Weryfikacja tabeli co_duty_notifications:
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'co_duty_notifications';

-- Weryfikacja indeksów dla co_duty_notifications:
-- SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'co_duty_notifications';

-- Weryfikacja polityk RLS dla co_duty_notifications:
-- SELECT policyname, cmd, qual, with_check FROM pg_policies WHERE tablename = 'co_duty_notifications';

-- Liczba oczekujących powiadomień (test):
-- SELECT COUNT(*) FROM public.co_duty_notifications WHERE to_doctor_id = auth.uid() AND status = 'pending';
