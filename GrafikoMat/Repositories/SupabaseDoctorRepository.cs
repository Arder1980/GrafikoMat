using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Repositories
{
    // ZMIANA: Klasa DoctorDto została usunięta, ponieważ jest już niepotrzebna.

    public class SupabaseDoctorRepository : IDoctorRepository
    {
        private readonly Client _client;

        public SupabaseDoctorRepository(Client client)
        {
            _client = client;
        }

        public async Task<IEnumerable<Doctor>> GetAllAsync()
        {
            // ZMIANA: Używamy bezpośrednio klasy Doctor
            var response = await _client.From<Doctor>().Get();
            return response.Models;
        }

        public async Task AddAsync(Doctor doctor)
        {
            // ZMIANA: Wstawiamy bezpośrednio obiekt Doctor
            await _client.From<Doctor>().Insert(doctor);
        }

        public Task<Doctor?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Doctor doctor)
        {
            throw new NotImplementedException();
        }
    }
}