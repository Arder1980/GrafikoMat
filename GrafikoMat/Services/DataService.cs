using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrafikoMat.Core.Data;
using GrafikoMat.ViewModels;
using Supabase;
using SbClient = Supabase.Client;

namespace GrafikoMat.Services
{
    public class DataService
    {
        private readonly SbClient _supabase;

        public DataService(SupabaseService supabaseService)
        {
            if (supabaseService.Client is null)
                throw new InvalidOperationException("Supabase client is not initialized.");

            _supabase = supabaseService.Client;
        }

        public async Task<List<DoctorProfile>> GetAllDoctorsAsync()
        {
            var response = await _supabase.From<DoctorProfile>().Get();
            return response.Models ?? new List<DoctorProfile>();
        }

        public async Task<List<Unit>> GetAllUnitsAsync()
        {
            var response = await _supabase.From<Unit>().Get();
            return response.Models ?? new List<Unit>();
        }

        public async Task<List<UnitDoctorAssignment>> GetAssignmentsForDoctorAsync(Guid doctorId)
        {
            var response = await _supabase.From<UnitDoctorAssignment>()
                .Where(x => x.DoctorId == doctorId)
                .Get();

            return response.Models ?? new List<UnitDoctorAssignment>();
        }

        public async Task SaveDoctorAsync(DoctorEditorViewModel editorViewModel)
        {
            var profile = editorViewModel.Profile;

            if (profile.Id == Guid.Empty)
            {
                // W Twojej wersji SDK IGoTrueClient nie ma SignUpWithPasswordAsync – używamy SignUp(...)
                await _supabase.Auth.SignUp(profile.Email, "GrafikoMat#");

                var userId = _supabase.Auth.CurrentUser?.Id;
                if (string.IsNullOrWhiteSpace(userId))
                    throw new Exception("Nie udało się utworzyć użytkownika w Supabase Auth (brak CurrentUser).");

                profile.Id = Guid.Parse(userId);
                await _supabase.From<DoctorProfile>().Insert(profile);
            }
            else
            {
                await _supabase.From<DoctorProfile>().Update(profile);
            }

            var currentAssignments = await GetAssignmentsForDoctorAsync(profile.Id);
            var desiredAssignments = editorViewModel.Assignments;

            var assignmentsToAdd = desiredAssignments
                .Where(d => d.IsAssigned && !currentAssignments.Any(c => c.UnitId == d.UnitId))
                .Select(d => new UnitDoctorAssignment
                {
                    DoctorId = profile.Id,
                    UnitId = d.UnitId,
                    IsActive = d.IsActive
                })
                .ToList();

            if (assignmentsToAdd.Any())
                await _supabase.From<UnitDoctorAssignment>().Insert(assignmentsToAdd);

            var assignmentsToRemove = currentAssignments
                .Where(c => !desiredAssignments.Any(d => d.UnitId == c.UnitId && d.IsAssigned));

            foreach (var toRemove in assignmentsToRemove)
                await _supabase.From<UnitDoctorAssignment>().Delete(toRemove);

            var assignmentsToUpdate = desiredAssignments
                .Where(d => d.IsAssigned &&
                            currentAssignments.Any(c => c.UnitId == d.UnitId && c.IsActive != d.IsActive))
                .Select(d => new UnitDoctorAssignment
                {
                    DoctorId = profile.Id,
                    UnitId = d.UnitId,
                    IsActive = d.IsActive
                });

            foreach (var toUpdate in assignmentsToUpdate)
                await _supabase.From<UnitDoctorAssignment>().Update(toUpdate);
        }

        public async Task SaveUnitAsync(Unit unit) =>
            await _supabase.From<Unit>().Upsert(unit);

        public async Task DeleteUnitAsync(Guid unitId) =>
            await _supabase.From<Unit>().Where(u => u.Id == unitId).Delete();
    }
}
