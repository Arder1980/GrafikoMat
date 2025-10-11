using System;
using System.Collections.Generic;

namespace GrafikoMat.Common
{
    public static class PolishHolidays
    {
        // ZMIANA: Rozbudowano słownik o dodatkowe święta i dni okolicznościowe
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

            // Dni okolicznościowe i popularne
            { (1, 21), "Dzień Babci" },
            { (1, 22), "Dzień Dziadka" },
            { (2, 14), "Walentynki" },
            { (3, 8),  "Dzień Kobiet" },
            { (4, 1),  "Prima Aprilis" },
            { (5, 26), "Dzień Matki" },
            { (6, 1),  "Dzień Dziecka" },
            { (6, 23), "Dzień Ojca" },
            { (10, 31),"Halloween" },
            { (11, 2), "Zaduszki" },
            { (11, 30),"Andrzejki" },
            { (12, 6), "Mikołajki" },
            { (12, 31),"Sylwester" },

            // Początki pór roku
            { (3, 21), "Początek wiosny" },
            { (6, 22), "Początek lata" },
            { (9, 23), "Początek jesieni" },
            { (12, 22),"Początek zimy" }
        };

        public static string? GetHolidayName(DateTime date)
        {
            // 1. Sprawdzenie świąt o stałej dacie
            if (_fixedHolidays.TryGetValue((date.Month, date.Day), out var holidayName))
            {
                return holidayName;
            }

            // 2. Obliczanie Wielkanocy (algorytm Gaussa) i świąt ruchomych
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

            // Święta wolne zależne od Wielkanocy
            if (date.Date == easterSunday.Date) return "Wielkanoc";
            if (date.Date == easterSunday.AddDays(1).Date) return "Pon. Wielkanocny";
            if (date.Date == easterSunday.AddDays(49).Date) return "Zielone Świątki";
            if (date.Date == easterSunday.AddDays(60).Date) return "Boże Ciało";

            // ZMIANA: Dodatkowe dni ruchome
            if (date.Date == easterSunday.AddDays(-52).Date) return "Tłusty Czwartek";
            if (date.Date == easterSunday.AddDays(-46).Date) return "Środa Popielcowa";
            if (date.Date == easterSunday.AddDays(-7).Date) return "Niedziela Palmowa";
            if (date.Date == easterSunday.AddDays(-3).Date) return "Wielki Czwartek";
            if (date.Date == easterSunday.AddDays(-2).Date) return "Wielki Piątek";
            if (date.Date == easterSunday.AddDays(-1).Date) return "Wielka Sobota";

            // 3. ZMIANA: Daty związane z rokiem szkolnym
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

            return null; // To nie jest święto
        }
    }
}