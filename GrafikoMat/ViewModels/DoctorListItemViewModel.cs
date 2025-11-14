using CommunityToolkit.Mvvm.ComponentModel;
using GrafikoMat.Core.Data;
using System;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// Reprezentuje pojedynczy, gotowy do wyświetlenia element na liście lekarzy.
    /// Zawiera logikę decydującą, czy do nazwy należy dodać skrót w nawiasach.
    /// </summary>
    public partial class DoctorListItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private DoctorProfile _profile;

        [ObservableProperty]
        private string _displayName;

        public bool IsArchived => Profile.IsArchived;
        public Guid Id => Profile.Id;

        // ================== NOWA WŁAŚCIWOŚĆ ==================
        /// <summary>
        /// Prawda, jeśli ten element listy reprezentuje aktualnie zalogowanego użytkownika.
        /// </summary>
        public bool IsCurrentUser { get; }
        // ======================================================

        // Pomocnicza właściwość do sortowania
        public string SortableName => $"{Profile.LastName} {Profile.FirstName}";

        // ================== ZMIANA W KONSTRUKTORZE ==================
        public DoctorListItemViewModel(DoctorProfile profile, bool needsDifferentiator, bool isCurrentUser)
        {
            _profile = profile;
            IsCurrentUser = isCurrentUser; // <-- Zapamiętujemy informację

            _displayName = needsDifferentiator
                ? $"{profile.LastName} {profile.FirstName} ({profile.Abbreviation})"
                : $"{profile.LastName} {profile.FirstName}";
        }

        partial void OnProfileChanged(DoctorProfile value)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsArchived));
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(SortableName));
        }
    }
}