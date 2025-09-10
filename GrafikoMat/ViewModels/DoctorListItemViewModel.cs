using GrafikoMat.Core.Data;
using System;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// Reprezentuje pojedynczy, gotowy do wyświetlenia element na liście lekarzy.
    /// Zawiera logikę decydującą, czy do nazwy należy dodać skrót w nawiasach.
    /// </summary>
    public class DoctorListItemViewModel
    {
        public DoctorProfile Profile { get; }
        public string DisplayName { get; }
        public bool IsArchived => Profile.IsArchived;
        public Guid Id => Profile.Id;

        // Pomocnicza właściwość do sortowania
        public string SortableName => $"{Profile.LastName} {Profile.FirstName}";

        public DoctorListItemViewModel(DoctorProfile profile, bool needsDifferentiator)
        {
            Profile = profile;

            DisplayName = needsDifferentiator
                ? $"{profile.LastName} {profile.FirstName} ({profile.Abbreviation})"
                : $"{profile.LastName} {profile.FirstName}";
        }
    }
}