using GrafikoMat.Core.Data;
// Usunięto: using GrafikoMat.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Definiuje kontrakt dla operacji na danych lekarzy (dyżurnych).
    /// </summary>
    public interface IDoctorRepository
    {
        /// <summary>
        /// Pobiera profil zalogowanego użytkownika.
        /// </summary>
        Task<DoctorProfile?> GetCurrentDoctorProfileAsync();

        /// <summary>
        /// Pobiera listę wszystkich profili lekarzy z bazy danych.
        /// </summary>
        Task<List<DoctorProfile>> GetAllAsync();

        /// <summary>
        /// Zapisuje zmiany w profilu lekarza oraz jego powiązania z jednostkami.
        /// </summary>
        /// <param name="profile">Główny profil lekarza do zapisu/aktualizacji.</param>
        /// <param name="assignments">Kolekcja przypisań, która ma zostać zapisana w bazie danych.</param>
        Task SaveAsync(DoctorProfile profile, IEnumerable<UnitDoctorAssignment> assignments);

        /// <summary>
        /// Ustawia status archiwizacji dla danego lekarza.
        /// </summary>
        Task SetArchiveStatusAsync(Guid doctorId, bool isArchived);

        /// <summary>
        /// Wywołuje procedurę resetowania hasła dla użytkownika (operacja administracyjna).
        /// </summary>
        Task ResetPasswordAsync(Guid doctorId, string newPassword);

        /// <summary>
        /// Ustawia flagę wymagającą od użytkownika zmiany hasła przy następnym logowaniu.
        /// </summary>
        Task SetPasswordChangeFlagAsync(Guid doctorId, bool requiresChange);

        /// <summary>
        /// Usuwa flagę wymagającą zmiany hasła.
        /// </summary>
        Task ClearPasswordChangeFlagAsync(Guid doctorId);
    }
}