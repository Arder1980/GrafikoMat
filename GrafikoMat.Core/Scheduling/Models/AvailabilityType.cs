namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Definiuje możliwe typy dostępności (deklaracji) lekarza w danym dniu.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public enum AvailabilityType
    {
        /// <summary>
        /// Lekarz jest niedostępny (domyślny stan).
        /// </summary>
        Unavailable,

        /// <summary>
        /// Lekarz jest dostępny bez specjalnych preferencji.
        /// </summary>
        Available,

        /// <summary>
        /// Lekarz preferuje dyżur w tym dniu.
        /// </summary>
        Wants,

        /// <summary>
        /// Lekarz jest na urlopie (twarda blokada).
        /// </summary>
        Vacation,

        /// <summary>
        /// Lekarz ma dyżur w innym miejscu (twarda blokada + blokada dni sąsiednich).
        /// </summary>
        OtherDuty,

        /// <summary>
        /// Lekarz może wziąć dyżur, ale tylko jeden taki w miesiącu.
        /// </summary>
        ConditionallyAvailable,

        /// <summary>
        /// Dzień jest zarezerwowany dla tego lekarza (twarde przypisanie, dostępne dla admina).
        /// </summary>
        Reservation
    }
}