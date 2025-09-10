using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
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
        private readonly IDoctorRepository _doctorRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly IAssignmentRepository _assignmentRepository;
        private readonly SupabaseService _supabaseService;
        private readonly DispatcherQueue? _dispatcher;
        private readonly IUxActionOrchestrator _orchestrator;
        private Guid _viewId;

        private readonly List<DoctorProfile> _allDoctorsMasterList = new();
        public ObservableCollection<DoctorListItemViewModel> FilteredDoctors { get; } = new();
        private List<Unit> _allUnits = new();

        private DoctorListItemViewModel? _selectedDoctor;
        public DoctorListItemViewModel? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                {
                    LoadEditorFor(value?.Profile);
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
            set { if (SetProperty(ref _searchText, value)) { FilterDoctors(); } }
        }

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetProperty(ref _showArchived, value)) { FilterDoctors(); } }
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
                if (_editorViewModel != null) _editorViewModel.PropertyChanged -= Editor_PropertyChanged;
                if (SetProperty(ref _editorViewModel, value))
                {
                    if (_editorViewModel != null) _editorViewModel.PropertyChanged += Editor_PropertyChanged;
                    SaveDoctorCommand.NotifyCanExecuteChanged();
                    ResetPasswordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand AddNewDoctorCommand { get; }
        public IAsyncRelayCommand SaveDoctorCommand { get; }
        public IAsyncRelayCommand ResetPasswordCommand { get; }
        public IAsyncRelayCommand ArchiveDoctorCommand { get; }
        public IAsyncRelayCommand RestoreDoctorCommand { get; }

        public ManagementViewModel(IDoctorRepository doctorRepo, IUnitRepository unitRepo, IAssignmentRepository assignmentRepo, SupabaseService supabaseService, DispatcherQueue? dispatcher)
        {
            _doctorRepository = doctorRepo;
            _unitRepository = unitRepo;
            _assignmentRepository = assignmentRepo;
            _supabaseService = supabaseService;
            _dispatcher = dispatcher;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();

            AddNewDoctorCommand = new AsyncRelayCommand(AddNewDoctorAsync);
            SaveDoctorCommand = new AsyncRelayCommand(SaveDoctorAsync, () => EditorViewModel?.IsValid ?? false);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => IsDoctorSelectedAndNotNew && (EditorViewModel?.ShowResetButton ?? false));
            ArchiveDoctorCommand = new AsyncRelayCommand(ArchiveDoctorAsync, () => CanArchive);
            RestoreDoctorCommand = new AsyncRelayCommand(RestoreDoctorAsync, () => CanRestore);
        }

        public void SetViewId(Guid viewId) => _viewId = viewId;
        public async Task InitializeAsync()
        {
            await _orchestrator.PerformLoadAsync(_viewId, LoadInitialDataAsync);
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorViewModel.IsValid)) SaveDoctorCommand.NotifyCanExecuteChanged();
            if (e.PropertyName == nameof(EditorViewModel.ShowResetButton)) ResetPasswordCommand.NotifyCanExecuteChanged();
        }

        private async Task EnsureUnitsLoadedAsync()
        {
            if (!_allUnits.Any())
            {
                _allUnits = await _unitRepository.GetAllAsync();
            }
        }

        private async Task LoadInitialDataAsync()
        {
            var previouslySelectedId = SelectedDoctor?.Id;
            var doctors = await _doctorRepository.GetAllAsync();
            _allUnits = await _unitRepository.GetAllAsync();

            _dispatcher?.TryEnqueue(() =>
            {
                _allDoctorsMasterList.Clear();
                _allDoctorsMasterList.AddRange(doctors);
                FilterDoctors();

                if (previouslySelectedId != null)
                {
                    SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == previouslySelectedId);
                }
            });
        }

        private void FilterDoctors()
        {
            FilteredDoctors.Clear();
            var sourceList = ShowArchived ? _allDoctorsMasterList : _allDoctorsMasterList.Where(d => !d.IsArchived);
            var filteredResult = (string.IsNullOrWhiteSpace(SearchText)
                ? sourceList
                : sourceList.Where(d =>
                    d.FullName.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();
            var duplicateFullNames = filteredResult
                .GroupBy(d => d.FullName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();
            foreach (var doctor in filteredResult.OrderBy(d => d.LastName).ThenBy(d => d.FirstName))
            {
                bool needsDifferentiator = duplicateFullNames.Contains(doctor.FullName);
                FilteredDoctors.Add(new DoctorListItemViewModel(doctor, needsDifferentiator));
            }
        }

        private async Task AddNewDoctorAsync()
        {
            // Używamy orkiestratora tylko do pokazania błędu, jeśli nie ma jednostek
            try
            {
                await _orchestrator.PerformLoadAsync(_viewId, async () =>
                {
                    await EnsureUnitsLoadedAsync();
                    if (!_allUnits.Any())
                    {
                        throw new Exception("Nie można dodać dyżurnego, ponieważ w systemie nie zdefiniowano żadnych jednostek. Dodaj je w Ustawieniach.");
                    }
                });
            }
            catch
            {
                return; // Orkiestrator pokazał błąd, przerywamy
            }

            SelectedDoctor = null;
            EditorViewModel = new DoctorEditorViewModel(
                new DoctorProfile { Id = Guid.Empty },
                new List<Unit>(_allUnits),
                new List<UnitDoctorAssignment>(),
                _allDoctorsMasterList.Select(d => d.Abbreviation)
            );
        }

        private async Task SaveDoctorAsync()
        {
            if (EditorViewModel == null || !EditorViewModel.IsValid) return;

            // Sprawdzanie, czy są nowe, niezapisane przypisania
            var newAssignments = EditorViewModel.Assignments.Where(a => a.IsAssigned && !a.IsPersisted).ToList();
            if (newAssignments.Any())
            {
                var dialog = new ContentDialog
                {
                    Title = "Potwierdź przypisanie do jednostki",
                    Content = "Przypisanie dyżurnego do jednostki jest operacją nieodwracalną z poziomu interfejsu użytkownika.\nCzy na pewno chcesz kontynuować?",
                    PrimaryButtonText = "Tak",
                    CloseButtonText = "Nie",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.MainRoot.Content.XamlRoot
                };
                var result = await dialog.ShowAsync();

                if (result != ContentDialogResult.Primary)
                {
                    ResetNewUnitAssignments();
                    return;
                }
            }

            var isNew = EditorViewModel.IsNewDoctor;
            var profile = EditorViewModel.Profile;
            var password = EditorViewModel.Password;
            var savedProfileId = profile.Id;

            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () =>
                {
                    if (isNew)
                    {
                        await _supabaseService.Client.Auth.SignUp(profile.Email, password);
                        var userId = _supabaseService.Client.Auth.CurrentUser?.Id;
                        if (string.IsNullOrWhiteSpace(userId))
                            throw new Exception("Nie udało się utworzyć użytkownika w Supabase Auth (brak CurrentUser).");

                        profile.Id = Guid.Parse(userId);
                        profile.RequiresPasswordChange = true;
                        await _supabaseService.Client.From<DoctorProfile>().Insert(profile);
                        savedProfileId = profile.Id;
                    }

                    var desiredAssignments = EditorViewModel.Assignments
                        .Where(a => a.IsAssigned)
                        .Select(a => new UnitDoctorAssignment { DoctorId = profile.Id, UnitId = a.UnitId, IsActive = a.IsActive });

                    await _doctorRepository.SaveAsync(profile, desiredAssignments);
                },
                verificationAsync: async () =>
                {
                    await LoadInitialDataAsync();
                    return _allDoctorsMasterList.Any(d => d.Id == (isNew ? savedProfileId : profile.Id));
                },
                successMessage: isNew ? "Nowy dyżurny został pomyślnie dodany." : "Poprawnie zapisano dane w bazie Supabase.",
                errorMessageTitle: "Błąd zapisu"
            );

            _dispatcher?.TryEnqueue(() =>
            {
                SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == (isNew ? savedProfileId : profile.Id));
            });
        }

        // NOWA METODA
        private void ResetNewUnitAssignments()
        {
            if (EditorViewModel?.Assignments != null)
            {
                foreach (var assignment in EditorViewModel.Assignments)
                {
                    if (!assignment.IsPersisted)
                    {
                        assignment.IsAssigned = false;
                    }
                }
            }
        }

        private async Task ArchiveDoctorAsync()
        {
            if (SelectedDoctor == null) return;
            var dialog = new ContentDialog
            {
                Title = "Potwierdź archiwizację",
                Content = $"Czy na pewno chcesz zarchiwizować profil lekarza {SelectedDoctor.DisplayName}?",
                PrimaryButtonText = "Archiwizuj",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainRoot.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var doctorToArchiveId = SelectedDoctor.Id;
            var doctorToArchiveName = SelectedDoctor.DisplayName;
            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () => await _doctorRepository.SetArchiveStatusAsync(doctorToArchiveId, true),
                verificationAsync: async () =>
                {
                    await LoadInitialDataAsync();
                    var reloaded = _allDoctorsMasterList.FirstOrDefault(d => d.Id == doctorToArchiveId);
                    return reloaded?.IsArchived ?? true;
                },
                successMessage: $"Profil lekarza {doctorToArchiveName} został zarchiwizowany.",
                errorMessageTitle: "Błąd archiwizacji"
            );
            SelectedDoctor = null;
        }

        private async Task RestoreDoctorAsync()
        {
            if (SelectedDoctor == null) return;
            var restoredDoctorId = SelectedDoctor.Id;
            var restoredDoctorName = SelectedDoctor.DisplayName;

            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () => await _doctorRepository.SetArchiveStatusAsync(restoredDoctorId, false),
                verificationAsync: async () =>
                {
                    await LoadInitialDataAsync();
                    return _allDoctorsMasterList.FirstOrDefault(d => d.Id == restoredDoctorId)?.IsArchived == false;
                },
                successMessage: $"Profil lekarza {restoredDoctorName} został przywrócony.",
                errorMessageTitle: "Błąd przywracania"
            );
            SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == restoredDoctorId);
        }

        private async Task ResetPasswordAsync()
        {
            if (SelectedDoctor == null || EditorViewModel == null) return;
            var dialog = new ContentDialog
            {
                Title = "Potwierdź resetowanie hasła",
                Content = $"Czy na pewno chcesz zresetować hasło dla użytkownika {SelectedDoctor.DisplayName}?",
                PrimaryButtonText = "Resetuj",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainRoot.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var newPassword = PasswordGenerator.GenerateInitialPassword();
            var doctorToResetId = SelectedDoctor.Id;
            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () =>
                {
                    await _doctorRepository.ResetPasswordAsync(doctorToResetId, newPassword);
                    await _doctorRepository.SetPasswordChangeFlagAsync(doctorToResetId, true);
                },
                verificationAsync: async () =>
                {
                    await LoadInitialDataAsync();
                    return _allDoctorsMasterList.FirstOrDefault(d => d.Id == doctorToResetId)?.RequiresPasswordChange ?? false;
                },
                successMessage: $"Nowe hasło startowe: {newPassword}",
                errorMessageTitle: "Błąd resetowania hasła"
            );
            EditorViewModel.SetNewGeneratedPassword(newPassword);
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

            await EnsureUnitsLoadedAsync();
            if (!_allUnits.Any())
            {
                return;
            }

            var existingAbbreviations = _allDoctorsMasterList
                .Where(d => d.Id != doctorProfile.Id)
                .Select(d => d.Abbreviation);
            var currentAssignments = await _assignmentRepository.GetForDoctorAsync(doctorProfile.Id);

            EditorViewModel = new DoctorEditorViewModel(
                (DoctorProfile)doctorProfile.Clone(),
                new List<Unit>(_allUnits),
                currentAssignments,
                existingAbbreviations
            );
        }
    }
}