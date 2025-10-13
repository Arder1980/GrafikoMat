using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    public interface IDoctorRepository
    {
        Task<DoctorProfile?> GetCurrentDoctorProfileAsync();
        Task<List<DoctorProfile>> GetAllAsync();
        Task SaveAsync(DoctorProfile profile, IEnumerable<UnitDoctorAssignment> assignments);
        Task SetArchiveStatusAsync(Guid doctorId, bool isArchived);
        Task ResetPasswordAsync(Guid doctorId, string newPassword);
        Task SetPasswordChangeFlagAsync(Guid doctorId, bool requiresChange);
        Task ClearPasswordChangeFlagAsync(Guid doctorId);

        // ================== NOWA METODA ==================
        /// <summary>
        /// Tworzy nowego użytkownika i jego profil w bazie danych.
        /// </summary>
        /// <param name="profile">Dane profilu do utworzenia.</param>
        /// <param name="password">Hasło startowe dla nowego użytkownika.</param>
        /// <returns>ID nowo utworzonego użytkownika.</returns>
        Task<Guid> CreateDoctorAsync(DoctorProfile profile, string password);
        // ===============================================
    }
}