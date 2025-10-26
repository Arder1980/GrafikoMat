using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Interfejs repozytorium do zarządzania deklaracjami dyżurowymi.
    /// </summary>
    public interface IDeclarationRepository
    {
        /// <summary>
        /// Pobiera wszystkie deklaracje dla danej jednostki w danym miesiącu.
        /// </summary>
        /// <param name="unitId">ID jednostki</param>
        /// <param name="year">Rok (np. 2025)</param>
        /// <param name="month">Miesiąc (1-12)</param>
        /// <returns>Lista deklaracji</returns>
        Task<List<Declaration>> GetDeclarationsForUnitMonthAsync(Guid unitId, int year, int month);

        /// <summary>
        /// Pobiera deklarację konkretnego lekarza dla danej jednostki i miesiąca.
        /// </summary>
        /// <param name="unitId">ID jednostki</param>
        /// <param name="doctorId">ID lekarza</param>
        /// <param name="year">Rok (np. 2025)</param>
        /// <param name="month">Miesiąc (1-12)</param>
        /// <returns>Deklaracja lub null jeśli nie istnieje</returns>
        Task<Declaration?> GetDeclarationForDoctorAsync(Guid unitId, Guid doctorId, int year, int month);

        /// <summary>
        /// Zapisuje lub aktualizuje deklarację.
        /// </summary>
        /// <param name="declaration">Deklaracja do zapisania</param>
        /// <returns>Zaktualizowana deklaracja z ID</returns>
        Task<Declaration> SaveDeclarationAsync(Declaration declaration);

        /// <summary>
        /// Usuwa deklarację po ID.
        /// </summary>
        /// <param name="declarationId">ID deklaracji</param>
        Task DeleteDeclarationAsync(long declarationId);

        /// <summary>
        /// Usuwa konkretny dzień z deklaracji lekarza.
        /// </summary>
        /// <param name="doctorId">ID lekarza</param>
        /// <param name="unitId">ID jednostki</param>
        /// <param name="year">Rok</param>
        /// <param name="month">Miesiąc</param>
        /// <param name="day">Dzień</param>
        /// <param name="slotPart">Część dnia ("full", "day", "night")</param>
        Task DeleteDayDeclarationAsync(Guid doctorId, Guid unitId, int year, int month, int day, string slotPart);
    }
}
