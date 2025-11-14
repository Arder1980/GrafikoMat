using GrafikoMat.Core.Data;
using GrafikoMat.Core.Enums;
using GrafikoMat.Core.Repositories;
using GrafikoMat.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Serwis do zarządzania powiadomieniami o współdyżurach.
    /// </summary>
    public class CoDutyNotificationService
    {
        private readonly ICoDutyNotificationRepository _notificationRepo;
        private readonly IDeclarationRepository _declarationRepo;
        private readonly IDoctorRepository _doctorRepo;
        private readonly IUnitRepository _unitRepo;

        public event EventHandler? NotificationCountChanged;

        public CoDutyNotificationService(
            ICoDutyNotificationRepository notificationRepo,
            IDeclarationRepository declarationRepo,
            IDoctorRepository doctorRepo,
            IUnitRepository unitRepo)
        {
            _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
            _declarationRepo = declarationRepo ?? throw new ArgumentNullException(nameof(declarationRepo));
            _doctorRepo = doctorRepo ?? throw new ArgumentNullException(nameof(doctorRepo));
            _unitRepo = unitRepo ?? throw new ArgumentNullException(nameof(unitRepo));
        }

        /// <summary>
        /// Pobiera wszystkie oczekujące powiadomienia dla lekarza jako ViewModele.
        /// </summary>
        public async Task<List<CoDutyNotificationViewModel>> GetPendingNotificationsAsync(Guid doctorId)
        {
            var notifications = await _notificationRepo.GetPendingNotificationsAsync(doctorId).ConfigureAwait(false);
            if (!notifications.Any())
                return new List<CoDutyNotificationViewModel>();

            var doctors = await _doctorRepo.GetAllAsync().ConfigureAwait(false);
            var units = await _unitRepo.GetAllAsync().ConfigureAwait(false);

            var viewModels = new List<CoDutyNotificationViewModel>();

            foreach (var notification in notifications)
            {
                var doctor = doctors.FirstOrDefault(d => d.Id == notification.FromDoctorId);
                var unit = units.FirstOrDefault(u => u.Id == notification.UnitId);

                if (doctor != null && unit != null)
                {
                    viewModels.Add(new CoDutyNotificationViewModel(
                        notification,
                        doctor.FullName,
                        doctor.Abbreviation,
                        unit.Name
                    ));
                }
            }

            return viewModels;
        }

        /// <summary>
        /// Pobiera liczbę oczekujących powiadomień.
        /// </summary>
        public async Task<int> GetPendingCountAsync(Guid doctorId)
        {
            return await _notificationRepo.GetPendingCountAsync(doctorId).ConfigureAwait(false);
        }

        /// <summary>
        /// Akceptuje powiadomienie - aktualizuje status deklaracji obu lekarzy na "accepted" i usuwa powiadomienie.
        /// </summary>
        public async Task<bool> AcceptNotificationAsync(long notificationId)
        {
            var notification = await _notificationRepo.GetNotificationByIdAsync(notificationId).ConfigureAwait(false);
            if (notification == null)
                return false;

            // Aktualizuj status w deklaracjach obu lekarzy na "accepted"
            await UpdateBothDeclarationsStatusAsync(notification, CoDutyStatus.Accepted).ConfigureAwait(false);

            // Usuń powiadomienie
            await _notificationRepo.DeleteNotificationAsync(notificationId).ConfigureAwait(false);

            NotificationCountChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Odrzuca powiadomienie - czyści deklaracje obu lekarzy i usuwa powiadomienie.
        /// </summary>
        public async Task<bool> RejectNotificationAsync(long notificationId)
        {
            var notification = await _notificationRepo.GetNotificationByIdAsync(notificationId).ConfigureAwait(false);
            if (notification == null)
                return false;

            // Wyczyść deklaracje obu lekarzy
            await ClearBothDeclarationsAsync(notification).ConfigureAwait(false);

            // Usuń powiadomienie
            await _notificationRepo.DeleteNotificationAsync(notificationId).ConfigureAwait(false);

            NotificationCountChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Aktualizuje status współdyżuru w deklaracjach obu lekarzy.
        /// </summary>
        private async Task UpdateBothDeclarationsStatusAsync(CoDutyNotification notification, CoDutyStatus status)
        {
            // Pobierz deklaracje obu lekarzy
            var initiatorDecl = await _declarationRepo.GetDeclarationForDoctorAsync(
                notification.UnitId,
                notification.FromDoctorId,
                notification.Year,
                notification.Month).ConfigureAwait(false);

            var partnerDecl = await _declarationRepo.GetDeclarationForDoctorAsync(
                notification.UnitId,
                notification.ToDoctorId,
                notification.Year,
                notification.Month).ConfigureAwait(false);

            if (initiatorDecl?.DeclarationDataJson?.Days == null ||
                partnerDecl?.DeclarationDataJson?.Days == null)
                return;

            // Zaktualizuj status w obu deklaracjach
            var initiatorDay = initiatorDecl.DeclarationDataJson.Days.FirstOrDefault(d => d.Day == notification.Day);
            var partnerDay = partnerDecl.DeclarationDataJson.Days.FirstOrDefault(d => d.Day == notification.Day);

            if (initiatorDay != null)
            {
                initiatorDay.CoDutyStatus = status;
            }

            if (partnerDay != null)
            {
                partnerDay.CoDutyStatus = status;
            }

            // Zapisz obie deklaracje
            await _declarationRepo.SaveDeclarationAsync(initiatorDecl).ConfigureAwait(false);
            await _declarationRepo.SaveDeclarationAsync(partnerDecl).ConfigureAwait(false);
        }

        /// <summary>
        /// Czyści deklaracje współdyżuru u obu lekarzy.
        /// </summary>
        private async Task ClearBothDeclarationsAsync(CoDutyNotification notification)
        {
            // Usuń deklarację dla dnia u inicjatora
            await _declarationRepo.DeleteDayDeclarationAsync(
                notification.FromDoctorId,
                notification.UnitId,
                notification.Year,
                notification.Month,
                notification.Day,
                notification.SlotPart.ToString().ToLowerInvariant()).ConfigureAwait(false);

            // Usuń deklarację dla dnia u partnera
            await _declarationRepo.DeleteDayDeclarationAsync(
                notification.ToDoctorId,
                notification.UnitId,
                notification.Year,
                notification.Month,
                notification.Day,
                notification.SlotPart.ToString().ToLowerInvariant()).ConfigureAwait(false);
        }
    }
}
