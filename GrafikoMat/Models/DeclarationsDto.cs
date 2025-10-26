using System;

namespace GrafikoMat.Models
{
    public enum DayMode { Full24, Split12 }

    public sealed class DayDeclaration
    {
        public DayMode Mode { get; set; } = DayMode.Full24;
        public string? Full { get; set; }      // dla 24h
        public string? Day { get; set; }       // dla 12h – dzień
        public string? Night { get; set; }     // dla 12h – noc

        // Współdyżurni
        public Guid? CoDutyPartnerId { get; set; }
        public string? CoDutyStatus { get; set; }  // "pending", "accepted", "rejected"
        public Guid? CoDutyInitiatorId { get; set; }
        public string? CoDutySlotPart { get; set; }  // "full", "day", "night" - który slot ma współdyżurnego
    }

    public sealed class DoctorMonthDeclaration
    {
        public string Doctor { get; set; } = "";
        public int Year { get; set; }
        public int MonthIndex { get; set; } // 0..11
        public DayDeclaration[] Days { get; set; } = System.Array.Empty<DayDeclaration>();
    }
}
