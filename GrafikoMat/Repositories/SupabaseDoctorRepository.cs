using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using Supabase;
using Supabase.Functions;
using Supabase.Gotrue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Supabase.Functions.Client;
using SbClient = Supabase.Client;

namespace GrafikoMat.Repositories
{
    public class SupabaseDoctorRepository : IDoctorRepository
    {
        private readonly SupabaseService _supabaseService;
        private SbClient _supabase => _supabaseService.Client!;

        public SupabaseDoctorRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
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

        // ================== NOWA METODA: Implementacja tworzenia użytkownika ==================
        public async Task<Guid> CreateDoctorAsync(DoctorProfile profile, string password)
        {
            if (_supabaseService.Client == null)
            {
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");
            }

            var rpcParams = new
            {
                p_email = profile.Email,
                p_password = password,
                p_first_name = profile.FirstName,
                p_last_name = profile.LastName,
                p_abbreviation = profile.Abbreviation,
                p_admin_level = profile.AdminLevel
            };

            var newUserIdString = await _supabaseService.Client.Rpc<string>("create_new_user", rpcParams);

            if (string.IsNullOrWhiteSpace(newUserIdString))
                throw new Exception("Nie udało się utworzyć użytkownika w Supabase (funkcja RPC nie zwróciła ID).");

            return Guid.Parse(newUserIdString);
        }
        // ======================================================================================

        public async Task SaveAsync(DoctorProfile profile, IEnumerable<UnitDoctorAssignment> desiredAssignments)
        {
            // Zmieniamy logikę: ta metoda służy już tylko do aktualizacji, nie do tworzenia
            if (profile.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Do tworzenia nowych użytkowników należy używać metody CreateDoctorAsync.");
            }

            var doctorDataForUpdate = new DoctorForUpdate
            {
                Id = profile.Id,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Abbreviation = profile.Abbreviation,
                Email = profile.Email,
                AdminLevel = profile.AdminLevel,
                IsArchived = profile.IsArchived,
                RequiresPasswordChange = profile.RequiresPasswordChange
            };
            await _supabase.From<DoctorForUpdate>()
                .Where(d => d.Id == profile.Id)
                .Update(doctorDataForUpdate);

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
            await _supabase.From<DoctorProfile>()
                .Where(d => d.Id == doctorId)
                .Set(d => d.IsArchived, isArchived)
                .Update();
        }

        public async Task ResetPasswordAsync(Guid doctorId, string newPassword)
        {
            var session = _supabase.Auth.CurrentSession;
            if (session?.AccessToken == null)
            {
                throw new InvalidOperationException("Brak aktywnej sesji użytkownika. Nie można wywołać funkcji chronionej.");
            }

            var parameters = new
            {
                user_id = doctorId,
                password_to_set = newPassword
            };
            var jsonPayload = JsonSerializer.Serialize(parameters);

            var options = new InvokeFunctionOptions
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {session.AccessToken}" }
                }
            };

            await _supabase.Functions.Invoke("admin-reset-user-password", jsonPayload, options);
        }

        public async Task SetPasswordChangeFlagAsync(Guid doctorId, bool requiresChange)
        {
            await _supabase.From<DoctorProfile>()
                .Where(d => d.Id == doctorId)
                .Set(d => d.RequiresPasswordChange, requiresChange)
                .Update();
        }

        public async Task ClearPasswordChangeFlagAsync(Guid doctorId)
        {
            await _supabase.From<DoctorProfile>()
                .Where(d => d.Id == doctorId)
                .Set(d => d.RequiresPasswordChange, false)
                .Update();
        }
    }
}