using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Interfejs repozytorium do zarządzania powiadomieniami o współdyżurach.
    /// </summary>
    public interface ICoDutyNotificationRepository
    {
        /// <summary>
        /// Pobiera wszystkie oczekujące powiadomienia dla danego lekarza.
        /// </summary>
        /// <param name="doctorId">ID lekarza odbierającego powiadomienia</param>
        /// <returns>Lista oczekujących powiadomień</returns>
        Task<List<CoDutyNotification>> GetPendingNotificationsAsync(Guid doctorId);

        /// <summary>
        /// Pobiera liczbę oczekujących powiadomień dla danego lekarza.
        /// </summary>
        /// <param name="doctorId">ID lekarza</param>
        /// <returns>Liczba oczekujących powiadomień</returns>
        Task<int> GetPendingCountAsync(Guid doctorId);

        /// <summary>
        /// Tworzy nowe powiadomienie o współdyżurze.
        /// </summary>
        /// <param name="notification">Powiadomienie do utworzenia</param>
        /// <returns>Utworzone powiadomienie z ID</returns>
        Task<CoDutyNotification> CreateNotificationAsync(CoDutyNotification notification);

        /// <summary>
        /// Aktualizuje status powiadomienia.
        /// </summary>
        /// <param name="notificationId">ID powiadomienia</param>
        /// <param name="status">Nowy status ("pending", "accepted", "rejected")</param>
        Task UpdateNotificationStatusAsync(long notificationId, string status);

        /// <summary>
        /// Usuwa powiadomienie po ID.
        /// </summary>
        /// <param name="notificationId">ID powiadomienia</param>
        Task DeleteNotificationAsync(long notificationId);

        /// <summary>
        /// Usuwa wszystkie powiadomienia związane z konkretną deklaracją.
        /// </summary>
        /// <param name="doctorId">ID lekarza (nadawcy lub odbiorcy)</param>
        /// <param name="unitId">ID jednostki</param>
        /// <param name="year">Rok</param>
        /// <param name="month">Miesiąc</param>
        /// <param name="day">Dzień</param>
        /// <param name="slotPart">Część dnia ("full", "day", "night")</param>
        Task DeleteByDeclarationAsync(Guid doctorId, Guid unitId, int year, int month, int day, string slotPart);

        /// <summary>
        /// Pobiera powiadomienie po ID.
        /// </summary>
        /// <param name="notificationId">ID powiadomienia</param>
        /// <returns>Powiadomienie lub null jeśli nie istnieje</returns>
        Task<CoDutyNotification?> GetNotificationByIdAsync(long notificationId);
    }
}
