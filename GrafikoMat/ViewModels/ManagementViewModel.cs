using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class ManagementViewModel : ObservableObject
    {
        private readonly DataService? _dataService;
        private readonly DispatcherQueue? _dispatcher;

        private readonly List<DoctorProfile> _allDoctorsMasterList = new();
        public ObservableCollection<DoctorProfile> FilteredDoctors { get; } = new();
        private List<Unit> _allUnits = new();

        private DoctorProfile? _selectedDoctor;
        public DoctorProfile? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                {
                    LoadEditorFor(value);
                    ResetPasswordCommand.NotifyCanExecuteChanged();
                    ArchiveDoctorCommand.NotifyCanExecuteChanged();
                    RestoreDoctorCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CanArchive));
                    OnPropertyChanged(nameof(CanRestore));
                }
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterDoctors();
                }
            }
        }

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set
            {
                if (SetProperty(ref _showArchived, value))
                {
                    FilterDoctors();
                }
            }
        }

        public bool CanArchive => SelectedDoctor != null && !SelectedDoctor.IsArchived;
        public bool CanRestore => SelectedDoctor != null && SelectedDoctor.IsArchived;


        public bool IsDoctorSelectedAndNotNew => SelectedDoctor != null && (EditorViewModel != null && !EditorViewModel.IsNewDoctor);
        private DoctorEditorViewModel? _editorViewModel;
        public DoctorEditorViewModel? EditorViewModel
        {
            get => _editorViewModel;
            set
            {
                if (_editorViewModel != null)
                {
                    _editorViewModel.PropertyChanged -= Editor_PropertyChanged;
                }
                if (SetProperty(ref _editorViewModel, value))
                {
                    if (_editorViewModel != null)
                    {
                        _editorViewModel.PropertyChanged += Editor_PropertyChanged;
                    }
                    SaveDoctorCommand.NotifyCanExecuteChanged();
                    ResetPasswordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    // ZMIANA: Powiadom o zmianie właściwości zależnej
                    OnPropertyChanged(nameof(ShowProgressRing));
                }
            }
        }

        private bool _isStatusMessageOpen;
        public bool IsStatusMessageOpen
        {
            get => _isStatusMessageOpen;
            set
            {
                if (SetProperty(ref _isStatusMessageOpen, value))
                {
                    // ZMIANA: Powiadom o zmianie właściwości zależnej
                    OnPropertyChanged(nameof(ShowProgressRing));
                }
            }
        }

        // ZMIANA: Dodana brakująca właściwość
        public bool ShowProgressRing => IsLoading && !IsStatusMessageOpen;

        private string _statusMessageTitle = string.Empty;
        public string StatusMessageTitle { get => _statusMessageTitle; set => SetProperty(ref _statusMessageTitle, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private InfoBarSeverity _statusMessageSeverity;
        public InfoBarSeverity StatusMessageSeverity { get => _statusMessageSeverity; set => SetProperty(ref _statusMessageSeverity, value); }

        public AsyncRelayCommand AddNewDoctorCommand { get; }
        public AsyncRelayCommand SaveDoctorCommand { get; }
        public AsyncRelayCommand ResetPasswordCommand { get; }
        public AsyncRelayCommand ArchiveDoctorCommand { get; }
        public AsyncRelayCommand RestoreDoctorCommand { get; }

        public ManagementViewModel(DataService? dataService, DispatcherQueue? dispatcher)
        {
            _dataService = dataService;
            _dispatcher = dispatcher;

            AddNewDoctorCommand = new AsyncRelayCommand(AddNewDoctorAsync);
            SaveDoctorCommand = new AsyncRelayCommand(SaveDoctor, () => EditorViewModel?.IsValid ?? false);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPassword, () => IsDoctorSelectedAndNotNew && (EditorViewModel?.ShowResetButton ?? false));
            ArchiveDoctorCommand = new AsyncRelayCommand(ArchiveDoctor, () => CanArchive);
            RestoreDoctorCommand = new AsyncRelayCommand(RestoreDoctor, () => CanRestore);
        }

        public async Task InitializeAsync()
        {
            await LoadInitialDataAsync();
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorViewModel.IsValid))
            {
                SaveDoctorCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName == nameof(EditorViewModel.ShowResetButton))
            {
                ResetPasswordCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task EnsureUnitsLoadedAsync()
        {
            if (_allUnits.Count > 0 || _dataService == null) return;
            try
            {
                _allUnits = await _dataService.GetAllUnitsAsync();
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd krytyczny", $"Nie udało się wczytać listy jednostek: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async Task LoadInitialDataAsync()
        {
            if (_dataService == null) return;
            var previouslySelectedId = SelectedDoctor?.Id;

            try
            {
                var doctors = await _dataService.GetAllDoctorsAsync();
                _allUnits = await _dataService.GetAllUnitsAsync();

                _dispatcher?.TryEnqueue(() =>
                {
                    _allDoctorsMasterList.Clear();
                    _allDoctorsMasterList.AddRange(doctors.OrderBy(d => d.LastName));
                    FilterDoctors();

                    if (previouslySelectedId != null)
                    {
                        SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == previouslySelectedId);
                    }
                });
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd ładowania danych", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void FilterDoctors()
        {
            FilteredDoctors.Clear();
            var sourceList = ShowArchived ? _allDoctorsMasterList : _allDoctorsMasterList.Where(d => !d.IsArchived);
            var filteredResult = string.IsNullOrWhiteSpace(SearchText)
                ? sourceList
                : sourceList.Where(d =>
                    d.FullName.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase));
            foreach (var doctor in filteredResult)
            {
                FilteredDoctors.Add(doctor);
            }
        }

        private async Task AddNewDoctorAsync()
        {
            IsLoading = true;
            try
            {
                await EnsureUnitsLoadedAsync();
                if (_allUnits.Count == 0)
                {
                    ShowStatusMessage("Brak jednostek", "Nie udało się wczytać listy jednostek. Sprawdź połączenie w Ustawieniach.", InfoBarSeverity.Warning);
                    return;
                }

                SelectedDoctor = null;
                EditorViewModel = new DoctorEditorViewModel(
                    new DoctorProfile { Id = Guid.Empty },
                    new List<Unit>(_allUnits),
                    new List<UnitDoctorAssignment>(),
                    _allDoctorsMasterList.Select(d => d.Abbreviation)
                );
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveDoctor()
        {
            if (_dataService == null || EditorViewModel == null || !EditorViewModel.IsValid) return;
            HideStatusMessage();

            var newAssignments = EditorViewModel.Assignments
                .Where(a => a.IsAssigned && !a.IsPersisted)
                .ToList();
            if (newAssignments.Any())
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Potwierdzenie Operacji Nieodwracalnej",
                    Content = "Przypisanie lekarza do nowej jednostki jest operacją trwałą i nie będzie można jej cofnąć w przyszłości.\n\nCzy na pewno chcesz kontynuować?",
                    PrimaryButtonText = "Tak, zapisz przypisanie",
                    CloseButtonText = "Anuluj",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.MainRoot.Content.XamlRoot
                };
                var result = await confirmDialog.ShowAsync();

                if (result != ContentDialogResult.Primary) return;
            }

            IsLoading = true;
            string successMessage = string.Empty;
            try
            {
                var isNew = EditorViewModel.IsNewDoctor;
                var savedProfileId = EditorViewModel.Profile.Id;

                await _dataService.SaveDoctorAsync(EditorViewModel);
                await LoadInitialDataAsync();

                _dispatcher?.TryEnqueue(() =>
                {
                    SelectedDoctor = _allDoctorsMasterList.FirstOrDefault(d => d.Id == savedProfileId);
                });

                successMessage = isNew ? "Nowy dyżurny został pomyślnie dodany." : "Poprawnie zapisano dane w bazie Supabase.";
            }
            catch (Exception ex)
            {
                IsLoading = false;
                ShowStatusMessage("Błąd zapisu", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }

            if (!string.IsNullOrEmpty(successMessage))
            {
                await ShowTemporarySuccessMessage("Sukces!", successMessage);
            }
        }

        private async Task ArchiveDoctor()
        {
            if (SelectedDoctor == null || _dataService == null) return;
            var confirmDialog = new ContentDialog
            {
                Title = "Potwierdź archiwizację",
                Content = $"Czy na pewno chcesz zarchiwizować profil lekarza {SelectedDoctor.FullName}? Profil zostanie ukryty na listach, ale będzie można go przywrócić.",
                PrimaryButtonText = "Archiwizuj",
                CloseButtonText = "Anuluj",
                XamlRoot = App.MainRoot.Content.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            IsLoading = true;
            string archivedDoctorName = SelectedDoctor.FullName;
            bool success = false;
            try
            {
                await _dataService.SetDoctorArchiveStatusAsync(SelectedDoctor.Id, true);
                await LoadInitialDataAsync();
                SelectedDoctor = null;
                success = true;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                ShowStatusMessage("Błąd", $"Wystąpił błąd podczas archiwizacji: {ex.Message}", InfoBarSeverity.Error);
            }

            if (success)
            {
                await ShowTemporarySuccessMessage("Sukces", $"Profil lekarza {archivedDoctorName} został zarchiwizowany.");
            }
        }

        private async Task RestoreDoctor()
        {
            if (SelectedDoctor == null || _dataService == null) return;
            IsLoading = true;
            string restoredDoctorName = SelectedDoctor.FullName;
            Guid restoredDoctorId = SelectedDoctor.Id;
            bool success = false;
            try
            {
                await _dataService.SetDoctorArchiveStatusAsync(SelectedDoctor.Id, false);
                await LoadInitialDataAsync();
                SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == restoredDoctorId);
                success = true;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                ShowStatusMessage("Błąd", $"Wystąpił błąd podczas przywracania: {ex.Message}", InfoBarSeverity.Error);
            }

            if (success)
            {
                await ShowTemporarySuccessMessage("Sukces", $"Profil lekarza {restoredDoctorName} został przywrócony.");
            }
        }

        private async Task ResetPassword()
        {
            if (SelectedDoctor == null || EditorViewModel == null || _dataService == null) return;
            var confirmDialog = new ContentDialog
            {
                Title = "Potwierdź resetowanie hasła",
                Content = $"Czy na pewno chcesz zresetować hasło dla użytkownika {SelectedDoctor.FirstName} {SelectedDoctor.LastName}?",
                PrimaryButtonText = "Resetuj",
                CloseButtonText = "Anuluj",
                XamlRoot = App.MainRoot.Content.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                IsLoading = true;
                HideStatusMessage();
                try
                {
                    var newPassword = PasswordGenerator.GenerateInitialPassword();
                    await _dataService.ResetPasswordAsync(SelectedDoctor.Id, newPassword);
                    await _dataService.SetPasswordChangeFlagAsync(SelectedDoctor.Id);
                    EditorViewModel.SetNewGeneratedPassword(newPassword);
                    await ShowTemporarySuccessMessage("Hasło zresetowane", $"Nowe hasło startowe: {newPassword}");
                }
                catch (Exception ex)
                {
                    IsLoading = false;
                    ShowStatusMessage("Błąd resetowania hasła", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }

        private async void LoadEditorFor(DoctorProfile? doctorProfile)
        {
            if (doctorProfile == null)
            {
                if (EditorViewModel != null && !EditorViewModel.IsNewDoctor)
                {
                    EditorViewModel = null;
                }
                return;
            }

            IsLoading = true;
            try
            {
                if (_dataService == null) return;
                await EnsureUnitsLoadedAsync();
                if (_allUnits.Count == 0)
                {
                    ShowStatusMessage("Brak jednostek", "Nie udało się wczytać listy jednostek. Sprawdź połączenie w Ustawieniach.", InfoBarSeverity.Warning);
                    EditorViewModel = null;
                    return;
                }

                var existingAbbreviations = _allDoctorsMasterList
                    .Where(d => d.Id != doctorProfile.Id)
                    .Select(d => d.Abbreviation);
                var currentAssignments = await _dataService.GetAssignmentsForDoctorAsync(doctorProfile.Id);

                EditorViewModel = new DoctorEditorViewModel(
                    (DoctorProfile)doctorProfile.Clone(),
                    new List<Unit>(_allUnits),
                    currentAssignments,
                    existingAbbreviations
                );
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd ładowania edytora", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
                EditorViewModel = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowTemporarySuccessMessage(string title, string message)
        {
            _dispatcher?.TryEnqueue(() =>
            {
                StatusMessageTitle = title;
                StatusMessage = message;
                StatusMessageSeverity = InfoBarSeverity.Success;
                IsStatusMessageOpen = true;
            });
            await Task.Delay(3000);

            _dispatcher?.TryEnqueue(() =>
            {
                if (StatusMessageSeverity == InfoBarSeverity.Success)
                {
                    IsStatusMessageOpen = false;
                }
                IsLoading = false;
            });
        }

        private void ShowStatusMessage(string title, string message, InfoBarSeverity severity)
        {
            _dispatcher?.TryEnqueue(() =>
            {
                StatusMessageTitle = title;
                StatusMessage = message;
                StatusMessageSeverity = severity;
                IsStatusMessageOpen = true;
            });
        }

        public void HideStatusMessage()
        {
            _dispatcher?.TryEnqueue(() =>
            {
                IsStatusMessageOpen = false;
                StatusMessage = string.Empty;
                StatusMessageTitle = string.Empty;
            });
        }
    }
}