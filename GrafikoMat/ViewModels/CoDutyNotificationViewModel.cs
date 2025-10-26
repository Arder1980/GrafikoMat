using CommunityToolkit.Mvvm.ComponentModel;
using GrafikoMat.Core.Data;
using System;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// ViewModel dla pojedynczego powiadomienia o współdyżurze.
    /// </summary>
    public partial class CoDutyNotificationViewModel : ObservableObject
    {
        private readonly CoDutyNotification _notification;

        [ObservableProperty]
        private string _fromDoctorName = string.Empty;

        [ObservableProperty]
        private string _fromDoctorAbbreviation = string.Empty;

        [ObservableProperty]
        private string _unitName = string.Empty;

        [ObservableProperty]
        private string _dateText = string.Empty;

        [ObservableProperty]
        private string _slotTypeText = string.Empty;

        [ObservableProperty]
        private string _summaryText = string.Empty;

        public long Id => _notification.Id;
        public Guid FromDoctorId => _notification.FromDoctorId;
        public Guid ToDoctorId => _notification.ToDoctorId;
        public Guid UnitId => _notification.UnitId;
        public int Year => _notification.Year;
        public int Month => _notification.Month;
        public int Day => _notification.Day;
        public string SlotPart => _notification.SlotPart;

        public CoDutyNotificationViewModel(
            CoDutyNotification notification,
            string fromDoctorName,
            string fromDoctorAbbreviation,
            string unitName)
        {
            _notification = notification;
            FromDoctorName = fromDoctorName;
            FromDoctorAbbreviation = fromDoctorAbbreviation;
            UnitName = unitName;
            DateText = $"{notification.Day:D2}.{notification.Month:D2}.{notification.Year}";

            SlotTypeText = notification.SlotPart switch
            {
                "full" => "24h",
                "day" => "Dzienny (7-19)",
                "night" => "Nocny (19-7)",
                _ => notification.SlotPart
            };

            SummaryText = $"{fromDoctorName} zaprasza do współdyżuru";
        }
    }
}
