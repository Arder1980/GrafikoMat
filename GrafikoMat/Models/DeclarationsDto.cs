using System;
using GrafikoMat.Core.Enums;

namespace GrafikoMat.Models
{
    // Enum przeniesiony do GrafikoMat.Core.Enums.DayMode (type safety + współdzielony)

    public sealed class DayDeclaration
    {
        public DayMode Mode { get; set; } = DayMode.Full24;
        public string? Full { get; set; }      // dla 24h
        public string? Day { get; set; }       // dla 12h – dzień
        public string? Night { get; set; }     // dla 12h – noc

        // Współdyżurni
        public Guid? CoDutyPartnerId { get; set; }
        public CoDutyStatus? CoDutyStatus { get; set; }  // Pending, Accepted, Rejected
        public Guid? CoDutyInitiatorId { get; set; }
        public SlotPart? CoDutySlotPart { get; set; }  // Full, Day, Night - który slot ma współdyżurnego
    }

    public sealed class DoctorMonthDeclaration
    {
        public Guid DoctorId { get; set; }  // Unique ID lekarza
        public string Doctor { get; set; } = "";  // FullName (może być duplikat!)
        public int Year { get; set; }
        public int MonthIndex { get; set; } // 0..11
        public DayDeclaration[] Days { get; set; } = System.Array.Empty<DayDeclaration>();
    }
}
