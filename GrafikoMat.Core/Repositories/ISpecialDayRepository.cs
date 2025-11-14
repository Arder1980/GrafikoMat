using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Repository dla dni specjalnych (ferie zimowe, święta lokalne, itp.)
    /// </summary>
    public interface ISpecialDayRepository
    {
        /// <summary>
        /// Pobiera wszystkie dni specjalne dla danego roku
        /// </summary>
        /// <param name="year">Rok</param>
        /// <param name="unitId">ID jednostki (NULL = tylko globalne, wartość = globalne + dla tej jednostki)</param>
        Task<List<SpecialDay>> GetSpecialDaysForYearAsync(int year, Guid? unitId = null);

        /// <summary>
        /// Pobiera wszystkie dni specjalne danego typu dla roku
        /// </summary>
        Task<List<SpecialDay>> GetSpecialDaysByTypeAsync(int year, string type, Guid? unitId = null);

        /// <summary>
        /// Pobiera dzień specjalny po ID
        /// </summary>
        Task<SpecialDay?> GetSpecialDayByIdAsync(Guid id);

        /// <summary>
        /// Sprawdza czy dla danej daty istnieje dzień specjalny
        /// </summary>
        Task<SpecialDay?> GetSpecialDayForDateAsync(DateOnly date, Guid? unitId = null);

        /// <summary>
        /// Tworzy nowy dzień specjalny
        /// </summary>
        Task<SpecialDay> CreateSpecialDayAsync(SpecialDay specialDay);

        /// <summary>
        /// Aktualizuje dzień specjalny
        /// </summary>
        Task<SpecialDay> UpdateSpecialDayAsync(SpecialDay specialDay);

        /// <summary>
        /// Usuwa dzień specjalny
        /// </summary>
        Task DeleteSpecialDayAsync(Guid id);

        /// <summary>
        /// Pobiera lub tworzy ferie zimowe dla danego roku (globalne)
        /// </summary>
        Task<SpecialDay> GetOrCreateWinterHolidayAsync(int year, Guid? unitId = null);
    }
}
