using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Definiuje kontrakt dla operacji na danych jednostek organizacyjnych.
    /// </summary>
    public interface IUnitRepository
    {
        /// <summary>
        /// Pobiera wszystkie jednostki z bazy danych.
        /// </summary>
        Task<List<Unit>> GetAllAsync();

        /// <summary>
        /// Wyszukuje pierwszą jednostkę o dokładnie pasującej pełnej nazwie szpitala (używane do autouzupełniania).
        /// </summary>
        Task<Unit?> GetFirstByExactHospitalNameAsync(string fullName);

        /// <summary>
        /// Wyszukuje unikalną jednostkę, której nazwa szpitala zaczyna się od podanego ciągu znaków.
        /// </summary>
        Task<Unit?> GetUniqueByHospitalNameStartAsync(string partialName);

        /// <summary>
        /// Zapisuje nową lub aktualizuje istniejącą jednostkę.
        /// </summary>
        Task SaveAsync(Unit unit);

        /// <summary>
        /// Ustawia status archiwizacji dla danej jednostki.
        /// </summary>
        Task SetArchiveStatusAsync(Guid unitId, bool isArchived);
    }
}