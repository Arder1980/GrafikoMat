using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Reprezentuje kompletny zestaw danych wejściowych dla algorytmu generującego grafik.
    /// Wersja zaadaptowana z GrafikWPF (GrafikWejsciowy).
    /// </summary>
    public class ScheduleInput
    {
        /// <summary>
        /// Lista profili lekarzy biorących udział w grafiku.
        /// </summary>
        public List<DoctorProfile> Doctors { get; set; } = new();

        /// <summary>
        /// Słownik przechowujący zadeklarowaną dostępność lekarzy w danym miesiącu.
        /// Klucz: Data (DateTime), Wartość: Słownik (Symbol Lekarza -> AvailabilityType).
        /// </summary>
        public Dictionary<DateTime, Dictionary<string, AvailabilityType>> Availability { get; set; } = new();

        /// <summary>
        /// Słownik przechowujący limity dyżurów dla lekarzy w danym miesiącu.
        /// Klucz: Symbol Lekarza (Abbreviation), Wartość: Limit (int).
        /// </summary>
        public Dictionary<string, int> DutyLimits { get; set; } = new();

        /// <summary>
        /// Właściwość pomocnicza zwracająca posortowaną listę dni w miesiącu na podstawie kluczy w słowniku Dostępność.
        /// </summary>
        public List<DateTime> DaysInMonth => Availability.Keys.OrderBy(d => d).ToList();
    }
}