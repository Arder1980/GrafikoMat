using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Common
{
    public static class PolishHolidays
    {
        // Cache dla dynamicznych dni specjalnych (per rok + unitId)
        private static readonly Dictionary<(int year, Guid? unitId), List<SpecialDay>> _specialDaysCache = new();
        private static readonly Dictionary<(int Month, int Day), string> _fixedHolidays = new()
        {
            // Święta państwowe (wolne)
            { (1, 1),   "Nowy Rok" },
            { (1, 6),   "Trzech Króli" },
            { (5, 1),   "Święto Pracy" },
            { (5, 3),   "Święto Konstytucji 3 Maja" },
            { (8, 15),  "Wniebowzięcie NMP" },
            { (11, 1),  "Wszystkich Świętych" },
            { (11, 11), "Święto Niepodległości" },
            { (12, 25), "Boże Narodzenie" },
            { (12, 26), "Boże Narodzenie" },

            // Dni okolicznościowe i popularne (NIE SĄ WOLNE)
            { (1, 21), "Dzień Babci" },
            { (1, 22), "Dzień Dziadka" },
            { (2, 14), "Walentynki" },
            { (3, 8),  "Dzień Kobiet" },
            { (4, 1),  "Prima Aprilis" },
            { (5, 2),  "Dzień Flagi RP" }, // NOWY
            { (5, 26), "Dzień Matki" },
            { (6, 1),  "Dzień Dziecka" },
            { (6, 23), "Dzień Ojca" },
            { (9, 30), "Dzień Chłopaka" },
            { (10, 14),"Dzień Edukacji Narodowej" },
            { (10, 31),"Halloween" },
            { (11, 2), "Zaduszki" },
            // { (11, 20),"Międzynarodowy Dzień Praw Dziecka" }, // USUNIĘTY
            { (11, 30),"Andrzejki" },
            { (12, 6), "Mikołajki" },
            { (12, 24),"Wigilia" },
            { (12, 31),"Sylwester" },

            // Początki pór roku (NIE SĄ WOLNE)
            { (3, 21), "Początek wiosny" },
            { (6, 22), "Początek lata" },
            { (9, 23), "Początek jesieni" },
            { (12, 22),"Początek zimy" }
        };

        private static readonly HashSet<(int Month, int Day)> _publicHolidays = new()
        {
            (1, 1), (1, 6), (5, 1), (5, 3), (8, 15), (11, 1), (11, 11), (12, 25), (12, 26)
        };

        public static bool IsPublicHoliday(DateTime date)
        {
            if (_publicHolidays.Contains((date.Month, date.Day)))
            {
                return true;
            }

            int a = date.Year % 19;
            int b = date.Year % 4;
            int c = date.Year % 7;
            int d = (19 * a + 24) % 30;
            int e = (2 * b + 4 * c + 6 * d + 5) % 7;

            var easterSunday = new DateTime(date.Year, 3, 22).AddDays(d + e);
            if (d + e > 9)
            {
                easterSunday = new DateTime(date.Year, 4, d + e - 9);
            }

            if (date.Date == easterSunday.Date) return true;
            if (date.Date == easterSunday.AddDays(1).Date) return true;
            if (date.Date == easterSunday.AddDays(49).Date) return true;
            if (date.Date == easterSunday.AddDays(60).Date) return true;

            return false;
        }

        /// <summary>
        /// Ustawia cache dni specjalnych dla danego roku i jednostki.
        /// Powinno być wywoływane przez DeclarationsViewModel przy ładowaniu danych.
        /// </summary>
        public static void SetSpecialDaysCache(int year, Guid? unitId, List<SpecialDay> specialDays)
        {
            _specialDaysCache[(year, unitId)] = specialDays;
        }

        /// <summary>
        /// Czyści cache dni specjalnych.
        /// </summary>
        public static void ClearSpecialDaysCache()
        {
            _specialDaysCache.Clear();
        }

        /// <summary>
        /// Pobiera nazwę święta/dnia specjalnego dla danej daty.
        /// Sprawdza najpierw dni specjalne z bazy (ferie, święta lokalne), potem stałe polskie święta.
        /// </summary>
        /// <param name="date">Data do sprawdzenia</param>
        /// <param name="unitId">ID jednostki (opcjonalne - dla dni specjalnych per jednostka)</param>
        /// <returns>Krótka nazwa (max 15 znaków) lub null</returns>
        public static string? GetHolidayName(DateTime date, Guid? unitId = null)
        {
            // Sprawdź cache dni specjalnych (ferie, święta lokalne)
            if (_specialDaysCache.TryGetValue((date.Year, unitId), out var specialDays))
            {
                var dateOnly = DateOnly.FromDateTime(date);
                var specialDay = specialDays
                    .Where(sd => sd.IsActiveOn(dateOnly))
                    .OrderByDescending(sd => sd.UnitId.HasValue) // Priorytet: jednostkowe przed globalnymi
                    .FirstOrDefault();

                if (specialDay != null)
                    return specialDay.Name;
            }

            // Sprawdź stałe święta polskie
            if (_fixedHolidays.TryGetValue((date.Month, date.Day), out var holidayName))
            {
                return holidayName;
            }

            int a = date.Year % 19;
            int b = date.Year % 4;
            int c = date.Year % 7;
            int d = (19 * a + 24) % 30;
            int e = (2 * b + 4 * c + 6 * d + 5) % 7;

            var easterSunday = new DateTime(date.Year, 3, 22).AddDays(d + e);
            if (d + e > 9)
            {
                easterSunday = new DateTime(date.Year, 4, d + e - 9);
            }

            if (date.Date == easterSunday.Date) return "Wielkanoc";
            if (date.Date == easterSunday.AddDays(1).Date) return "Pon. Wielkanocny";
            if (date.Date == easterSunday.AddDays(49).Date) return "Zielone Świątki";
            if (date.Date == easterSunday.AddDays(60).Date) return "Boże Ciało";

            if (date.Date == easterSunday.AddDays(-52).Date) return "Tłusty Czwartek";
            if (date.Date == easterSunday.AddDays(-47).Date) return "Ostatki";
            if (date.Date == easterSunday.AddDays(-46).Date) return "Środa Popielcowa";
            if (date.Date == easterSunday.AddDays(-7).Date) return "Niedziela Palmowa";
            if (date.Date == easterSunday.AddDays(-3).Date) return "Wielki Czwartek";
            if (date.Date == easterSunday.AddDays(-2).Date) return "Wielki Piątek";
            if (date.Date == easterSunday.AddDays(-1).Date) return "Wielka Sobota";

            if (date.Month == 6)
            {
                var lastDayOfJune = new DateTime(date.Year, 6, 30);
                int daysBack = (int)lastDayOfJune.DayOfWeek - (int)DayOfWeek.Friday;
                if (daysBack < 0) daysBack += 7;
                var lastFriday = lastDayOfJune.AddDays(-daysBack);
                if (date.Date == lastFriday.Date) return "Zakończenie roku szkolnego";
            }

            if (date.Month == 9)
            {
                var septemberFirst = new DateTime(date.Year, 9, 1);
                var schoolStarts = septemberFirst;
                if (septemberFirst.DayOfWeek == DayOfWeek.Friday)
                    schoolStarts = septemberFirst.AddDays(3);
                else if (septemberFirst.DayOfWeek == DayOfWeek.Saturday)
                    schoolStarts = septemberFirst.AddDays(2);
                else if (septemberFirst.DayOfWeek == DayOfWeek.Sunday)
                    schoolStarts = septemberFirst.AddDays(1);

                if (date.Date == schoolStarts.Date) return "Rozpoczęcie roku szkolnego";
            }

            return null;
        }

        /// <summary>
        /// Pobiera pełną nazwę święta/dnia specjalnego (dla tooltip).
        /// </summary>
        public static string? GetFullHolidayName(DateTime date, Guid? unitId = null)
        {
            // Sprawdź cache dni specjalnych (ferie, święta lokalne)
            if (_specialDaysCache.TryGetValue((date.Year, unitId), out var specialDays))
            {
                var dateOnly = DateOnly.FromDateTime(date);
                var specialDay = specialDays
                    .Where(sd => sd.IsActiveOn(dateOnly))
                    .OrderByDescending(sd => sd.UnitId.HasValue)
                    .FirstOrDefault();

                if (specialDay != null)
                    return specialDay.FullName;
            }

            // Dla stałych świąt - zwróć krótką nazwę (nie mamy osobnej długiej)
            return GetHolidayName(date, unitId);
        }
    }
}