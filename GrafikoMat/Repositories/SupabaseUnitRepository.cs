using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SbClient = Supabase.Client;

namespace GrafikoMat.Repositories
{
    public class SupabaseUnitRepository : IUnitRepository
    {
        private readonly SbClient _supabase;

        public SupabaseUnitRepository(SbClient supabaseClient)
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
            // Pobierz wszystkie jednostki zaczynające się od podanego tekstu
            var response = await _supabase.From<Unit>()
                .Filter("hospital_full_name", Constants.Operator.ILike, $"{partialName}%")
                .Get();

            var matches = response.Models ?? new List<Unit>();

            if (!matches.Any())
                return null;

            // Sprawdź czy wszystkie pasujące jednostki mają tę samą nazwę szpitala
            var uniqueHospitalNames = matches
                .Select(u => u.HospitalFullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Jeśli jest dokładnie jedna unikalna nazwa szpitala, zwróć pierwszą jednostkę
            if (uniqueHospitalNames.Count == 1)
            {
                return matches.First();
            }

            return null;
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