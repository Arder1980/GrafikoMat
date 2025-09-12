using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SbClient = Supabase.Client;

namespace GrafikoMat.Repositories
{
    public class SupabaseDoctorRepository : IDoctorRepository
    {
        private readonly SbClient _supabase;

        public SupabaseDoctorRepository(SbClient supabaseClient)
        {
            _supabase = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
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

        public async Task<List<DoctorProfile>> GetAllAsync()
        {
            var response = await _supabase.From<DoctorProfile>().Get();
            return response.Models ?? new List<DoctorProfile>();
        }

        public async Task SaveAsync(DoctorProfile profile, IEnumerable<UnitDoctorAssignment> desiredAssignments)
        {
            if (profile.Id == Guid.Empty)
            {
                throw new NotImplementedException("Tworzenie nowych użytkowników wymaga oddzielnej logiki SignUp, która znajduje się w ViewModelu.");
            }
            else
            {
                var doctorDataForUpdate = new DoctorForUpdate
                {
                    Id = profile.Id,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    Abbreviation = profile.Abbreviation,
                    Email = profile.Email,
                    IsAdmin = profile.IsAdmin,
                    IsArchived = profile.IsArchived,
                    RequiresPasswordChange = profile.RequiresPasswordChange
                };

                await _supabase.From<DoctorForUpdate>().Update(doctorDataForUpdate);
            }

            var assignmentRepo = new SupabaseAssignmentRepository(_supabase);
            var currentAssignments = await assignmentRepo.GetForDoctorAsync(profile.Id);

            var assignmentsToAdd = desiredAssignments
                .Where(d => !currentAssignments.Any(c => c.UnitId == d.UnitId))
                .ToList();

            if (assignmentsToAdd.Any())
                await _supabase.From<UnitDoctorAssignment>().Insert(assignmentsToAdd);

            var assignmentsToRemove = currentAssignments
                .Where(c => !desiredAssignments.Any(d => d.UnitId == c.UnitId));

            foreach (var toRemove in assignmentsToRemove)
                await _supabase.From<UnitDoctorAssignment>().Delete(toRemove);
        }

        public async Task SetArchiveStatusAsync(Guid doctorId, bool isArchived)
        {
            var partialUpdate = new DoctorForUpdate { Id = doctorId, IsArchived = isArchived };
            await _supabase.From<DoctorForUpdate>().Update(partialUpdate);
        }

        public async Task ResetPasswordAsync(Guid doctorId, string newPassword)
        {
            await _supabase.Rpc("admin_reset_user_password", new
            {
                user_id = doctorId,
                password_to_set = newPassword
            });
        }

        public async Task SetPasswordChangeFlagAsync(Guid doctorId, bool requiresChange)
        {
            var partialUpdate = new DoctorForUpdate { Id = doctorId, RequiresPasswordChange = requiresChange };
            await _supabase.From<DoctorForUpdate>().Update(partialUpdate);
        }

        public async Task ClearPasswordChangeFlagAsync(Guid doctorId)
        {
            var partialUpdate = new DoctorForUpdate { Id = doctorId, RequiresPasswordChange = false };
            await _supabase.From<DoctorForUpdate>().Update(partialUpdate);
        }
    }
}