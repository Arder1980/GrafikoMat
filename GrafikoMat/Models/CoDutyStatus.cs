// UWAGA: Ten plik jest zachowany dla kompatybilności wstecznej
// Nowy enum znajduje się w GrafikoMat.Core.Enums.CoDutyStatus
// Użyj: using GrafikoMat.Core.Enums;

namespace GrafikoMat.Models
{
    /// <summary>
    /// PRZESTARZAŁE: Użyj GrafikoMat.Core.Enums.CoDutyStatus zamiast tego
    /// </summary>
    [System.Obsolete("Użyj GrafikoMat.Core.Enums.CoDutyStatus zamiast tego")]
    public enum CoDutyStatus
    {
        /// <summary>
        /// Oczekuje na akceptację partnera
        /// </summary>
        Pending = GrafikoMat.Core.Enums.CoDutyStatus.Pending,

        /// <summary>
        /// Zaakceptowane przez partnera
        /// </summary>
        Accepted = GrafikoMat.Core.Enums.CoDutyStatus.Accepted,

        /// <summary>
        /// Odrzucone przez partnera
        /// </summary>
        Rejected = GrafikoMat.Core.Enums.CoDutyStatus.Rejected
    }
}
