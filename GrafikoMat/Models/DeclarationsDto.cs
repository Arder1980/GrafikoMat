namespace GrafikoMat.Models
{
    public enum DayMode { Full24, Split12 }

    public sealed class DayDeclaration
    {
        public DayMode Mode { get; set; } = DayMode.Full24;
        public string? Full { get; set; }      // dla 24h
        public string? Day { get; set; }       // dla 12h – dzień
        public string? Night { get; set; }     // dla 12h – noc
    }

    public sealed class DoctorMonthDeclaration
    {
        public string Doctor { get; set; } = "";
        public int Year { get; set; }
        public int MonthIndex { get; set; } // 0..11
        public DayDeclaration[] Days { get; set; } = System.Array.Empty<DayDeclaration>();
    }
}
