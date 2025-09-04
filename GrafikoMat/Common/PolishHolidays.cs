using System;
using System.Collections.Generic;

namespace GrafikoMat.Common
{
    public static class PolishHolidays
    {
        private static readonly HashSet<(int, int)> _fixedHolidays = new HashSet<(int, int)>
        {
            (1, 1),   // Nowy Rok
            (1, 6),   // Trzech Króli
            (5, 1),   // Święto Pracy
            (5, 3),   // Święto Konstytucji 3 Maja
            (8, 15),  // Wniebowzięcie NMP
            (11, 1),  // Wszystkich Świętych
            (11, 11), // Święto Niepodległości
            (12, 25), // Boże Narodzenie
            (12, 26)  // Boże Narodzenie
        };

        public static bool IsHoliday(DateTime date)
        {
            if (_fixedHolidays.Contains((date.Month, date.Day)))
                return true;

            // Obliczanie Wielkanocy (algorytm Gaussa)
            int a = date.Year % 19;
            int b = date.Year % 4;
            int c = date.Year % 7;
            int d = (19 * a + 24) % 30;
            int e = (2 * b + 4 * c + 6 * d + 5) % 7;
            var easterSunday = new DateTime(date.Year, 3, 22).AddDays(d + e);

            if (date == easterSunday.AddDays(1)) // Poniedziałek Wielkanocny
                return true;

            if (date == easterSunday.AddDays(49)) // Zesłanie Ducha Świętego (Zielone Świątki)
                return true;

            if (date == easterSunday.AddDays(60)) // Boże Ciało
                return true;

            return false;
        }
    }
}