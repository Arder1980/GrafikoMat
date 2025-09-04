using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Definiuje kontrakt dla operacji na danych lekarzy.
    /// To jest abstrakcja, która pozwoli w przyszłości podmienić lokalne źródło danych na API online.
    /// </summary>
    public interface IDoctorRepository
    {
        Task<Doctor?> GetByIdAsync(Guid id);
        Task<IEnumerable<Doctor>> GetAllAsync();
        Task AddAsync(Doctor doctor);
        Task UpdateAsync(Doctor doctor);
    }
}