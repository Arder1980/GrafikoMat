using CommunityToolkit.Mvvm.ComponentModel;
using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// ViewModel dla dialogu wyboru współdyżurnego.
    /// </summary>
    public partial class SelectCoDutyPartnerViewModel : ObservableObject
    {
        private readonly List<DoctorProfile> _allDoctors;
        private readonly Guid _currentDoctorId;
        private readonly HashSet<Guid> _excludedDoctorIds; // Lekarze którzy mają już deklarację na ten slot

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DoctorListItemViewModel> _filteredDoctors = new();

        [ObservableProperty]
        private DoctorListItemViewModel? _selectedDoctor;

        public SelectCoDutyPartnerViewModel(
            List<DoctorProfile> allDoctors,
            Guid currentDoctorId,
            HashSet<Guid> excludedDoctorIds)
        {
            _allDoctors = allDoctors ?? throw new ArgumentNullException(nameof(allDoctors));
            _currentDoctorId = currentDoctorId;
            _excludedDoctorIds = excludedDoctorIds ?? new HashSet<Guid>();

            FilterDoctors();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterDoctors();
        }

        private void FilterDoctors()
        {
            var filtered = _allDoctors
                .Where(d => d.Id != _currentDoctorId) // Nie pokazuj siebie
                .Where(d => !d.IsArchived) // Nie pokazuj zarchiwizowanych
                .Where(d => !_excludedDoctorIds.Contains(d.Id)) // Nie pokazuj tych z deklaracjami
                .Where(d => string.IsNullOrWhiteSpace(SearchText)
                    || d.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || d.Abbreviation.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName)
                .Select(d => new DoctorListItemViewModel(d, needsDifferentiator: false, isCurrentUser: false))
                .ToList();

            FilteredDoctors = new ObservableCollection<DoctorListItemViewModel>(filtered);
        }
    }
}
