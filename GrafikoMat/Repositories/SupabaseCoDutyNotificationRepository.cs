using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SbClient = Supabase.Client;

namespace GrafikoMat.Repositories
{
    public class SupabaseCoDutyNotificationRepository : ICoDutyNotificationRepository
    {
        private readonly SupabaseService _supabaseService;
        private SbClient _supabase => _supabaseService.Client!;

        public SupabaseCoDutyNotificationRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        }

        public async Task<List<CoDutyNotification>> GetPendingNotificationsAsync(Guid doctorId)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            var response = await _supabase
                .From<CoDutyNotification>()
                .Where(n => n.ToDoctorId == doctorId)
                .Where(n => n.Status == "pending")
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            return response.Models ?? new List<CoDutyNotification>();
        }

        public async Task<int> GetPendingCountAsync(Guid doctorId)
        {
            var notifications = await GetPendingNotificationsAsync(doctorId);
            return notifications.Count;
        }

        public async Task<CoDutyNotification> CreateNotificationAsync(CoDutyNotification notification)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            notification.CreatedAt = DateTime.UtcNow;
            notification.Status = "pending";

            var response = await _supabase
                .From<CoDutyNotification>()
                .Insert(notification);

            var inserted = response.Models?.FirstOrDefault();
            if (inserted == null)
                throw new Exception("Nie udało się utworzyć powiadomienia.");

            return inserted;
        }

        public async Task UpdateNotificationStatusAsync(long notificationId, string status)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            var notification = new CoDutyNotification
            {
                Id = notificationId,
                Status = status,
                RespondedAt = DateTime.UtcNow
            };

            await _supabase
                .From<CoDutyNotification>()
                .Where(n => n.Id == notificationId)
                .Update(notification);
        }

        public async Task DeleteNotificationAsync(long notificationId)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            await _supabase
                .From<CoDutyNotification>()
                .Where(n => n.Id == notificationId)
                .Delete();
        }

        public async Task DeleteByDeclarationAsync(
            Guid doctorId,
            Guid unitId,
            int year,
            int month,
            int day,
            string slotPart)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            // Usuń powiadomienia gdzie lekarz jest nadawcą lub odbiorcą dla tej konkretnej deklaracji
            await _supabase
                .From<CoDutyNotification>()
                .Where(n => n.UnitId == unitId)
                .Where(n => n.Year == year)
                .Where(n => n.Month == month)
                .Where(n => n.Day == day)
                .Where(n => n.SlotPart == slotPart)
                .Filter("from_doctor_id", Supabase.Postgrest.Constants.Operator.Equals, doctorId.ToString())
                .Delete();

            await _supabase
                .From<CoDutyNotification>()
                .Where(n => n.UnitId == unitId)
                .Where(n => n.Year == year)
                .Where(n => n.Month == month)
                .Where(n => n.Day == day)
                .Where(n => n.SlotPart == slotPart)
                .Filter("to_doctor_id", Supabase.Postgrest.Constants.Operator.Equals, doctorId.ToString())
                .Delete();
        }

        public async Task<CoDutyNotification?> GetNotificationByIdAsync(long notificationId)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            try
            {
                var response = await _supabase
                    .From<CoDutyNotification>()
                    .Where(n => n.Id == notificationId)
                    .Limit(1)
                    .Single();

                return response;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
