using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Definiuje kontrakt dla operacji na danych lekarzy.
    /// </summary>
    public interface IDoctorRepository
    {
        Task<DoctorProfile?> GetByIdAsync(Guid id);
        Task<IEnumerable<DoctorProfile>> GetAllAsync();
        Task AddAsync(DoctorProfile doctor);
        Task UpdateAsync(DoctorProfile doctor);
    }
}