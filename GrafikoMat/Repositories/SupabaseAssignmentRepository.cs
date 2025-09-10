using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SbClient = Supabase.Client; // <-- NOWY ALIAS

namespace GrafikoMat.Repositories
{
    public class SupabaseAssignmentRepository : IAssignmentRepository
    {
        private readonly SbClient _supabase; // <-- ZMIANA TYPU

        public SupabaseAssignmentRepository(SbClient supabaseClient) // <-- ZMIANA TYPU
        {
            _supabase = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        }

        public async Task<List<UnitDoctorAssignment>> GetAllAsync()
        {
            var response = await _supabase.From<UnitDoctorAssignment>().Get();
            return response.Models ?? new List<UnitDoctorAssignment>();
        }

        public async Task<List<UnitDoctorAssignment>> GetForDoctorAsync(Guid doctorId)
        {
            var response = await _supabase.From<UnitDoctorAssignment>()
                .Where(x => x.DoctorId == doctorId)
                .Get();
            return response.Models ?? new List<UnitDoctorAssignment>();
        }
    }
}