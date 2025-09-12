using System;
using System.Collections.Generic;

namespace GrafikoMat.Common
{
    public static class PolishHolidays
    {
        // ZMIANA: Przechowujemy teraz nazwę święta razem z datą.
        private static readonly Dictionary<(int Month, int Day), string> _fixedHolidays = new()
        {
            { (1, 1),   "Nowy Rok" },
            { (1, 6),   "Trzech Króli" },
            { (5, 1),   "Święto Pracy" },
            { (5, 3),   "Święto Konstytucji 3 Maja" },
            { (8, 15),  "Wniebowzięcie NMP" },
            { (11, 1),  "Wszystkich Świętych" },
            { (11, 11), "Święto Niepodległości" },
            { (12, 25), "Boże Narodzenie" },
            { (12, 26), "Boże Narodzenie" }
        };

        // ZMIANA: Metoda zwraca teraz string? zamiast bool.
        // Zwraca nazwę święta lub null, jeśli to nie jest święto.
        public static string? GetHolidayName(DateTime date)
        {
            if (_fixedHolidays.TryGetValue((date.Month, date.Day), out var holidayName))
            {
                return holidayName;
            }

            // Obliczanie Wielkanocy (algorytm Gaussa)
            int a = date.Year % 19;
            int b = date.Year % 4;
            int c = date.Year % 7;
            int d = (19 * a + 24) % 30;
            int e = (2 * b + 4 * c + 6 * d + 5) % 7;
            // Korekta dla lat po 2099, jeśli będzie potrzebna.
            if (d + e > 9)
            {
                if (date.Day == d + e - 9 && date.Month == 4) { } // Niedziela Wielkanocna
            }
            else
            {
                if (date.Day == d + e + 22 && date.Month == 3) { } // Niedziela Wielkanocna
            }
            var easterSunday = new DateTime(date.Year, 3, 22).AddDays(d + e);

            if (date.Date == easterSunday.Date) return "Wielkanoc";
            if (date.Date == easterSunday.AddDays(1).Date) return "Pon. Wielkanocny";
            if (date.Date == easterSunday.AddDays(49).Date) return "Zielone Świątki";
            if (date.Date == easterSunday.AddDays(60).Date) return "Boże Ciało";

            return null; // To nie jest święto
        }
    }
}