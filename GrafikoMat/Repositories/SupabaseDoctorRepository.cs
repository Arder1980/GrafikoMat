using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase; // ZMIANA: Dodajemy dyrektywę using
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace GrafikoMat.Repositories
{
    public class SupabaseDoctorRepository : IDoctorRepository
    {
        private readonly Client _client;
        public SupabaseDoctorRepository(Client client)
        {
            _client = client;
        }

        public async Task<IEnumerable<DoctorProfile>> GetAllAsync()
        {
            var response = await _client.From<DoctorProfile>().Get();
            return response.Models;
        }

        public async Task AddAsync(DoctorProfile doctor)
        {
            await _client.From<DoctorProfile>().Insert(doctor);
        }

        public Task<DoctorProfile?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(DoctorProfile doctor)
        {
            throw new NotImplementedException();
        }
    }
}