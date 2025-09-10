using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SbClient = Supabase.Client; // <-- NOWY ALIAS

namespace GrafikoMat.Repositories
{
    public class SupabaseUnitRepository : IUnitRepository
    {
        private readonly SbClient _supabase; // <-- ZMIANA TYPU

        public SupabaseUnitRepository(SbClient supabaseClient) // <-- ZMIANA TYPU
        {
            _supabase = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        }

        public async Task<List<Unit>> GetAllAsync()
        {
            var response = await _supabase.From<Unit>().Get();
            return response.Models ?? new List<Unit>();
        }

        public async Task<Unit?> GetFirstByExactHospitalNameAsync(string fullName)
        {
            var response = await _supabase.From<Unit>()
                .Filter("hospital_full_name", Constants.Operator.Equals, fullName)
                .Limit(1)
                .Get();
            return response.Models?.FirstOrDefault();
        }

        public async Task<Unit?> GetUniqueByHospitalNameStartAsync(string partialName)
        {
            var response = await _supabase.From<Unit>()
                .Filter("hospital_full_name", Constants.Operator.ILike, $"{partialName}%")
                .Limit(2)
                .Get();

            return response.Models != null && response.Models.Count == 1
                ? response.Models.First()
                : null;
        }

        public async Task SaveAsync(Unit unit)
        {
            await _supabase.From<Unit>().Upsert(unit);
        }

        public async Task SetArchiveStatusAsync(Guid unitId, bool isArchived)
        {
            await _supabase.From<Unit>()
                .Where(u => u.Id == unitId)
                .Set(u => u.IsArchived, isArchived)
                .Update();
        }
    }
}