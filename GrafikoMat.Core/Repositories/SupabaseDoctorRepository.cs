using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Repositories
{
    /// <summary>
    /// Implementacja repozytorium lekarzy, która będzie komunikować się z API Supabase.
    /// </summary>
    public class SupabaseDoctorRepository : IDoctorRepository
    {
        // W przyszłości wstrzykniemy tutaj klienta Supabase
        // private readonly Supabase.Client _client;

        public Task AddAsync(Doctor doctor)
        {
            // TODO: Implement API call to insert a new doctor
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Doctor>> GetAllAsync()
        {
            // TODO: Implement API call to fetch all doctors
            throw new NotImplementedException();
        }

        public Task<Doctor?> GetByIdAsync(Guid id)
        {
            // TODO: Implement API call to fetch a single doctor by ID
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Doctor doctor)
        {
            // TODO: Implement API call to update a doctor
            throw new NotImplementedException();
        }
    }
}