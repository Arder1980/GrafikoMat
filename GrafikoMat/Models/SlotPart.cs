// UWAGA: Ten plik jest zachowany dla kompatybilności wstecznej
// Nowy enum znajduje się w GrafikoMat.Core.Enums.SlotPart
// Użyj: using GrafikoMat.Core.Enums;

namespace GrafikoMat.Models
{
    /// <summary>
    /// PRZESTARZAŁE: Użyj GrafikoMat.Core.Enums.SlotPart zamiast tego
    /// </summary>
    [System.Obsolete("Użyj GrafikoMat.Core.Enums.SlotPart zamiast tego")]
    public enum SlotPart
    {
        Full = GrafikoMat.Core.Enums.SlotPart.Full,
        Day = GrafikoMat.Core.Enums.SlotPart.Day,
        Night = GrafikoMat.Core.Enums.SlotPart.Night
    }
}
