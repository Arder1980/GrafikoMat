namespace GrafikoMat.Core.Enums
{
    /// <summary>
    /// Status współdyżuru między dwoma lekarzami
    /// </summary>
    public enum CoDutyStatus
    {
        /// <summary>
        /// Oczekuje na akceptację partnera
        /// </summary>
        Pending,

        /// <summary>
        /// Zaakceptowane przez partnera
        /// </summary>
        Accepted,

        /// <summary>
        /// Odrzucone przez partnera
        /// </summary>
        Rejected
    }
}
