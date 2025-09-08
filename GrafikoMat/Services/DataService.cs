using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.WriteLine($"[DataService.GetAllUnitsAsync] Stan klienta: {(_supabase == null ? "NULL" : "OK")}, Auth: {(_supabase?.Auth?.CurrentUser == null ? "Brak" : "OK")}, URL: {_supabase?.Postgrest.BaseUrl}");
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

        public async Task<DoctorProfile?> GetCurrentDoctorProfileAsync()
        {
            if (_supabase.Auth.CurrentUser?.Id is null) return null;
            var userId = Guid.Parse(_supabase.Auth.CurrentUser.Id);
            var response = await _supabase.From<DoctorProfile>()
                .Where(d => d.Id == userId)
                .Single();
            return response;
        }

        public async Task ClearPasswordChangeFlagAsync(Guid doctorId)
        {
            var partialUpdate = new DoctorProfile
            {
                Id = doctorId,
                RequiresPasswordChange = false
            };
            await _supabase.From<DoctorProfile>().Update(partialUpdate);
        }

        public async Task ResetPasswordAsync(Guid doctorId, string newPassword)
        {
            await _supabase.Rpc("admin_reset_user_password", new
            {
                user_id = doctorId,
                password_to_set = newPassword
            });
        }

        public async Task SetPasswordChangeFlagAsync(Guid doctorId)
        {
            var partialUpdate = new DoctorProfile
            {
                Id = doctorId,
                RequiresPasswordChange = true
            };
            await _supabase.From<DoctorProfile>().Update(partialUpdate);
        }

        // NOWA METODA: Do archiwizacji i przywracania lekarzy
        public async Task SetDoctorArchiveStatusAsync(Guid doctorId, bool isArchived)
        {
            var partialUpdate = new DoctorProfile
            {
                Id = doctorId,
                IsArchived = isArchived
            };
            await _supabase.From<DoctorProfile>().Update(partialUpdate);
        }

        public async Task SaveDoctorAsync(DoctorEditorViewModel editorViewModel)
        {
            var profile = editorViewModel.Profile;
            if (profile.Id == Guid.Empty)
            {
                await _supabase.Auth.SignUp(profile.Email, editorViewModel.Password);
                var userId = _supabase.Auth.CurrentUser?.Id;
                if (string.IsNullOrWhiteSpace(userId))
                    throw new Exception("Nie udało się utworzyć użytkownika w Supabase Auth (brak CurrentUser).");
                profile.Id = Guid.Parse(userId);
                profile.RequiresPasswordChange = true;

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
                await _supabase.From<UnitDoctorAssignment>().Insert(assignmentsToAdd);

            var assignmentsToRemove = currentAssignments
                .Where(c => !desiredAssignments.Any(d => d.UnitId == c.UnitId && d.IsAssigned));

            foreach (var toRemove in assignmentsToRemove)
                await _supabase.From<UnitDoctorAssignment>().Delete(toRemove);

            var assignmentsToUpdate = desiredAssignments
                .Where(d => d.IsAssigned && currentAssignments.Any(c => c.UnitId == d.UnitId && c.IsActive != d.IsActive))
                .Select(d => new UnitDoctorAssignment { DoctorId = profile.Id, UnitId = d.UnitId, IsActive = d.IsActive });

            foreach (var toUpdate in assignmentsToUpdate)
                await _supabase.From<UnitDoctorAssignment>().Update(toUpdate);
        }

        public async Task SaveUnitAsync(Unit unit) =>
            await _supabase.From<Unit>().Upsert(unit);
        public async Task DeleteUnitAsync(Guid unitId) =>
            await _supabase.From<Unit>().Where(u => u.Id == unitId).Delete();
    }
}