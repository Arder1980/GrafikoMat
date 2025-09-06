using GrafikoMat.Core.Data;
using GrafikoMat.ViewModels;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public class DataService
    {
        private readonly Client _supabase;

        public DataService(SupabaseService supabaseService)
        {
            if (supabaseService.Client is null)
            {
                throw new InvalidOperationException("Supabase client is not initialized.");
            }
            _supabase = supabaseService.Client;
        }

        #region Doctor Management

        public async Task<List<DoctorProfile>> GetAllDoctorsAsync()
        {
            var response = await _supabase.From<DoctorProfile>().Get();
            return response.Models ?? new List<DoctorProfile>();
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
                var session = await _supabase.Auth.SignUp(profile.Email, "GrafikoMat#");
                if (session?.User?.Id == null)
                {
                    throw new Exception("Failed to create user in Supabase Auth.");
                }

                profile.Id = new Guid(session.User.Id);
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
                .Select(d => new UnitDoctorAssignment { DoctorId = profile.Id, UnitId = d.UnitId, IsActive = d.IsActive })
                .ToList();

            if (assignmentsToAdd.Any())
            {
                await _supabase.From<UnitDoctorAssignment>().Insert(assignmentsToAdd);
            }

            var assignmentsToRemove = currentAssignments
                .Where(c => !desiredAssignments.Any(d => d.UnitId == c.UnitId && d.IsAssigned));

            foreach (var toRemove in assignmentsToRemove)
            {
                await _supabase.From<UnitDoctorAssignment>().Delete(toRemove);
            }

            var assignmentsToUpdate = desiredAssignments
                .Where(d => d.IsAssigned && currentAssignments.Any(c => c.UnitId == d.UnitId && c.IsActive != d.IsActive))
                .Select(d => new UnitDoctorAssignment { DoctorId = profile.Id, UnitId = d.UnitId, IsActive = d.IsActive });

            foreach (var toUpdate in assignmentsToUpdate)
            {
                await _supabase.From<UnitDoctorAssignment>().Update(toUpdate);
            }
        }

        #endregion

        #region Unit Management

        public async Task<List<Unit>> GetAllUnitsAsync()
        {
            var response = await _supabase.From<Unit>().Get();
            return response.Models ?? new List<Unit>();
        }

        public async Task SaveUnitAsync(Unit unit)
        {
            // Jeśli ID jest puste, to jest to nowa jednostka (INSERT),
            // w przeciwnym razie aktualizujemy istniejącą (UPDATE).
            await _supabase.From<Unit>().Upsert(unit);
        }

        public async Task DeleteUnitAsync(Guid unitId)
        {
            await _supabase.From<Unit>().Where(u => u.Id == unitId).Delete();
        }

        #endregion
    }
}