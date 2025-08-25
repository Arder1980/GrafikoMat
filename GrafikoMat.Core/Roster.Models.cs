using System;
using System.Collections.Generic;
using System.Linq;
using GrafikoMat.Core.Scheduling;

namespace GrafikoMat.Core.Rostering
{
    /// <summary>
    /// Wymagania personalne na slot (np. 1 osoba albo 2 osoby jako „para”).
    /// </summary>
    public sealed class StaffRequirement
    {
        /// <summary>Rok/miesiąc obowiązywania.</summary>
        public int Year { get; init; }
        public int Month { get; init; }

        /// <summary>Liczba wymaganych osób na każdy slot. 1 (domyślnie) lub 2 dla par dyżurnych.</summary>
        public int RequiredPersonsPerSlot { get; set; } = 1;

        public StaffRequirement(int year, int month, int requiredPersonsPerSlot = 1)
        {
            Year = year;
            Month = month;
            RequiredPersonsPerSlot = Math.Max(1, requiredPersonsPerSlot);
        }
    }

    /// <summary>
    /// Przydział na pojedynczy slot: lista lekarzy (rozmiar zależny od wymagań).
    /// </summary>
    public sealed class RosterAssignment
    {
        public DutySlot Slot { get; init; }
        public List<string> DoctorIds { get; } = new();

        public RosterAssignment(DutySlot slot, IEnumerable<string>? doctorIds = null)
        {
            Slot = slot;
            if (doctorIds != null) DoctorIds.AddRange(doctorIds);
        }
    }

    /// <summary>
    /// Wynikowy grafik dla miesiąca (zbiór przydziałów na sloty).
    /// </summary>
    public sealed class RosterMonth
    {
        public int Year { get; init; }
        public int Month { get; init; }

        public List<RosterAssignment> Assignments { get; } = new();

        public RosterMonth(int year, int month)
        {
            Year = year;
            Month = month;
        }

        /// <summary>Pomocniczo: zwraca przydziały dla konkretnej daty.</summary>
        public IEnumerable<RosterAssignment> ForDate(DateOnly date)
            => Assignments.Where(a => a.Slot.Date == date);
    }
}
