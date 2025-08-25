using System;
using System.Collections.Generic;

namespace GrafikoMat.Core.Scheduling
{
    /// <summary>
    /// Część doby reprezentowana przez slot dyżurowy.
    /// Full24 = dyżur całodobowy; Day12/Night12 = sloty 12-godzinne.
    /// </summary>
    public enum ShiftPart
    {
        Full24 = 0,
        Day12 = 1,
        Night12 = 2
    }

    /// <summary>
    /// Układ slotów dla danego dnia: albo jedna doba 24h, albo podział 12+12h.
    /// </summary>
    public enum SlotLayout
    {
        Full24,
        Split12x2
    }

    /// <summary>
    /// Pojedynczy slot dyżuru w kalendarzu (konkretny dzień + część doby).
    /// </summary>
    public readonly struct DutySlot
    {
        public DateOnly Date { get; }
        public ShiftPart Part { get; }

        public DutySlot(DateOnly date, ShiftPart part)
        {
            Date = date;
            Part = part;
        }

        public static DutySlot Full(DateOnly date) => new(date, ShiftPart.Full24);
        public static DutySlot Day(DateOnly date) => new(date, ShiftPart.Day12);
        public static DutySlot Night(DateOnly date) => new(date, ShiftPart.Night12);

        public override string ToString()
            => Part == ShiftPart.Full24
                ? $"{Date:yyyy-MM-dd} [24h]"
                : $"{Date:yyyy-MM-dd} [{(Part == ShiftPart.Day12 ? "Dzień" : "Noc")}]";
    }

    /// <summary>
    /// Układ miesięczny: definiuje, które dni są 24h, a które 12+12h.
    /// </summary>
    public sealed class MonthLayout
    {
        /// <summary>Rok kalendarzowy.</summary>
        public int Year { get; init; }

        /// <summary>Miesiąc (1-12).</summary>
        public int Month { get; init; }

        /// <summary>
        /// Gdy true — domyślnie wszystkie dni są 12+12h, a <see cref="FullDays"/> przywraca 24h.
        /// Gdy false — domyślnie wszystkie dni są 24h, a <see cref="SplitDays"/> wymusza 12+12h.
        /// </summary>
        public bool UseTwelveHourByDefault { get; init; }

        /// <summary>Dni (numer dnia miesiąca) wymuszające 12+12h, gdy domyślnie 24h.</summary>
        public HashSet<int> SplitDays { get; } = new();

        /// <summary>Dni (numer dnia miesiąca) wymuszające 24h, gdy domyślnie 12+12h.</summary>
        public HashSet<int> FullDays { get; } = new();

        public MonthLayout(int year, int month, bool useTwelveHourByDefault = false)
        {
            Year = year;
            Month = month;
            UseTwelveHourByDefault = useTwelveHourByDefault;
        }

        /// <summary>
        /// Czy dany dzień ma układ 12+12h (true) czy 24h (false).
        /// </summary>
        public bool IsSplit(DateOnly date)
        {
            if (date.Year != Year || date.Month != Month)
                throw new ArgumentOutOfRangeException(nameof(date), "Data poza zakresem układu miesiąca.");

            if (UseTwelveHourByDefault)
                return !FullDays.Contains(date.Day);

            return SplitDays.Contains(date.Day);
        }

        /// <summary>
        /// Zwraca wszystkie sloty (24h albo 12+12h) z tego miesiąca.
        /// </summary>
        public IEnumerable<DutySlot> EnumerateSlots()
        {
            int days = DateTime.DaysInMonth(Year, Month);
            for (int day = 1; day <= days; day++)
            {
                var date = new DateOnly(Year, Month, day);
                if (IsSplit(date))
                {
                    yield return DutySlot.Day(date);
                    yield return DutySlot.Night(date);
                }
                else
                {
                    yield return DutySlot.Full(date);
                }
            }
        }
    }
}
