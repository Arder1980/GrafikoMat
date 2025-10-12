using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

        public async Task SaveAsync(DoctorProfile profile, IEnumerable<UnitDoctorAssignment> desiredAssignments)
        {
            if (profile.Id == Guid.Empty)
            {
                throw new NotImplementedException("Tworzenie nowych użytkowników odbywa się poprzez dedykowaną funkcję RPC w ManagementViewModel.");
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
                await _supabase.From<DoctorForUpdate>()
                    .Where(d => d.Id == profile.Id)
                    .Update(doctorDataForUpdate);
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
                throw new InvalidOperationException("Brak aktywnej sesji administratora. Nie można zresetować hasła.");
            }

            if (string.IsNullOrEmpty(_supabaseService.SupabaseUrl) || string.IsNullOrEmpty(_supabaseService.SupabaseAnonKey))
            {
                throw new InvalidOperationException("Klient Supabase nie jest poprawnie zainicjalizowany (brak URL lub klucza).");
            }

            using var client = new HttpClient();
            var functionUrl = $"{_supabaseService.SupabaseUrl}/functions/v1/admin-reset-user-password";

            client.DefaultRequestHeaders.Add("apikey", _supabaseService.SupabaseAnonKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

            var parameters = new
            {
                user_id = doctorId,
                password_to_set = newPassword
            };
            var jsonPayload = JsonSerializer.Serialize(parameters);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(functionUrl, content);
            response.EnsureSuccessStatusCode();
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

        // ================== NOWA IMPLEMENTACJA ==================
        public async Task<Guid> CreateDoctorAsync(DoctorProfile profile, string password)
        {
            var session = _supabase.Auth.CurrentSession;
            if (session?.AccessToken == null)
            {
                throw new InvalidOperationException("Brak aktywnej sesji administratora. Nie można utworzyć użytkownika.");
            }

            if (string.IsNullOrEmpty(_supabaseService.SupabaseUrl) || string.IsNullOrEmpty(_supabaseService.SupabaseAnonKey))
            {
                throw new InvalidOperationException("Klient Supabase nie jest poprawnie zainicjalizowany (brak URL lub klucza).");
            }

            using var client = new HttpClient();
            var functionUrl = $"{_supabaseService.SupabaseUrl}/functions/v1/admin-create-user";

            // Ustawiamy wymagane nagłówki
            client.DefaultRequestHeaders.Add("apikey", _supabaseService.SupabaseAnonKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

            var parameters = new
            {
                p_email = profile.Email,
                p_password = password,
                p_first_name = profile.FirstName,
                p_last_name = profile.LastName,
                p_abbreviation = profile.Abbreviation,
                p_is_admin = profile.IsAdmin
            };

            var jsonPayload = JsonSerializer.Serialize(parameters);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(functionUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Błąd wywołania funkcji Edge: {response.StatusCode}. Treść: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var newUserId = JsonSerializer.Deserialize<string>(responseContent);

            if (string.IsNullOrEmpty(newUserId))
            {
                throw new Exception("Funkcja Edge nie zwróciła ID nowego użytkownika.");
            }

            return Guid.Parse(newUserId);
        }
        // ========================================================
    }
}