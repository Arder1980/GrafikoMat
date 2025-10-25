using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using GrafikoMat.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Supabase.Gotrue;
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
        private bool _isDirty = false;

        private readonly List<DoctorProfile> _allDoctorsMasterList = new();
        public ObservableCollection<DoctorListItemViewModel> FilteredDoctors { get; } = new();
        private List<Unit> _allUnits = new();

        private Guid _currentUserId;
        private int _currentUserLevel;

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
        public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) { FilterDoctors(); } } }

        private bool _showArchived;
        public bool ShowArchived { get => _showArchived; set { if (SetProperty(ref _showArchived, value)) { FilterDoctors(); } } }

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

        public void SetViewId(Guid viewId)
        {
            _viewId = viewId;
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] SetViewId called with: {viewId}");
        }

        /// <summary>
        /// Sprawdza czy są niezapisane zmiany.
        /// </summary>
        public bool HasUnsavedChanges => _isDirty;

        private void MarkAsDirty()
        {
            _isDirty = true;
            SaveDoctorCommand.NotifyCanExecuteChanged();
        }

        private void ClearDirty()
        {
            _isDirty = false;
        }

        public async Task InitializeAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] InitializeAsync called with _viewId: {_viewId}");
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] _orchestrator is null: {_orchestrator == null}");

            if (_orchestrator == null)
            {
                System.Diagnostics.Debug.WriteLine("[ManagementViewModel] ERROR: _orchestrator is null! Cannot perform load.");
                return;
            }

            await _orchestrator.PerformLoadAsync(_viewId, LoadInitialDataAsync);
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] InitializeAsync completed");
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Oznacz jako dirty przy zmianie wartości w edytorze (ale pomiń właściwości systemowe)
            if (e.PropertyName != nameof(EditorViewModel.IsValid) &&
                e.PropertyName != nameof(EditorViewModel.ShowResetButton))
            {
                MarkAsDirty();
            }

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
            System.Diagnostics.Debug.WriteLine("[ManagementViewModel] LoadInitialDataAsync START");
            var previouslySelectedId = SelectedDoctor?.Id;

            System.Diagnostics.Debug.WriteLine("[ManagementViewModel] Getting current doctor profile...");
            var currentUserProfile = await _doctorRepository.GetCurrentDoctorProfileAsync();
            if (currentUserProfile != null)
            {
                _currentUserId = currentUserProfile.Id;
                _currentUserLevel = currentUserProfile.AdminLevel;
                System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] Current user: {currentUserProfile.FirstName} {currentUserProfile.LastName}, Level: {currentUserProfile.AdminLevel}");
            }

            System.Diagnostics.Debug.WriteLine("[ManagementViewModel] Loading doctors...");
            var doctors = await _doctorRepository.GetAllAsync();
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] Loaded {doctors.Count} doctors");

            System.Diagnostics.Debug.WriteLine("[ManagementViewModel] Loading units...");
            _allUnits = await _unitRepository.GetAllAsync();
            System.Diagnostics.Debug.WriteLine($"[ManagementViewModel] Loaded {_allUnits.Count} units");

            _dispatcher?.TryEnqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ManagementViewModel] Updating UI on dispatcher thread");
                _allDoctorsMasterList.Clear();
                _allDoctorsMasterList.AddRange(doctors);
                FilterDoctors();

                if (previouslySelectedId != null)
                {
                    SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == previouslySelectedId);
                }
                System.Diagnostics.Debug.WriteLine("[ManagementViewModel] UI update complete");
            });

            System.Diagnostics.Debug.WriteLine("[ManagementViewModel] LoadInitialDataAsync END");
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
                bool isCurrentUser = doctor.Id == _currentUserId;
                FilteredDoctors.Add(new DoctorListItemViewModel(doctor, needsDifferentiator, isCurrentUser));
            }
        }

        private async Task AddNewDoctorAsync()
        {
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
            catch { return; }

            SelectedDoctor = null;
            EditorViewModel = new DoctorEditorViewModel(
                new DoctorProfile { Id = Guid.Empty },
                new List<Unit>(_allUnits),
                new List<UnitDoctorAssignment>(),
                _allDoctorsMasterList.Select(d => d.Abbreviation),
                _currentUserId,
                _currentUserLevel
            );
        }

        private async Task SaveDoctorAsync()
        {
            if (EditorViewModel == null || !EditorViewModel.IsValid) return;

            var profile = EditorViewModel.Profile;
            var isNew = EditorViewModel.IsNewDoctor;

            if (!isNew && profile.Id == _currentUserId && profile.AdminLevel < 9)
            {
                await ShowInfo("Błąd zapisu", "Nie można odebrać sobie uprawnień Superadministratora.");
                return;
            }

            var newAssignments = EditorViewModel.Assignments.Where(a => a.IsAssigned && !a.IsPersisted).ToList();
            if (newAssignments.Any())
            {
                var dialog = App.CreateThemedDialog();
                dialog.Title = "Potwierdź przypisanie do jednostki";
                dialog.Content = "Przypisanie dyżurnego do jednostki jest operacją nieodwracalną z poziomu interfejsu użytkownika.\nCzy na pewno chcesz kontynuować?";
                dialog.PrimaryButtonText = "Tak";
                dialog.CloseButtonText = "Nie";
                dialog.DefaultButton = ContentDialogButton.Close;

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    ResetNewUnitAssignments();
                    return;
                }
            }

            var password = EditorViewModel.Password;
            var savedProfileId = profile.Id;

            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () =>
                {
                    if (isNew)
                    {
                        // ================== ZMIANA: Wywołanie metody z repozytorium ==================
                        savedProfileId = await _doctorRepository.CreateDoctorAsync(profile, password);
                        profile.Id = savedProfileId; // Uaktualniamy ID w obiekcie profilu

                        var desiredAssignments = EditorViewModel.Assignments
                            .Where(a => a.IsAssigned)
                            .Select(a => new UnitDoctorAssignment { DoctorId = savedProfileId, UnitId = a.UnitId, IsActive = a.IsActive })
                            .ToList();

                        if (desiredAssignments.Any() && _supabaseService.Client != null)
                        {
                            await _supabaseService.Client.From<UnitDoctorAssignment>().Insert(desiredAssignments);
                        }
                    }
                    else
                    {
                        var desiredAssignments = EditorViewModel.Assignments
                            .Where(a => a.IsAssigned)
                            .Select(a => new UnitDoctorAssignment { DoctorId = profile.Id, UnitId = a.UnitId, IsActive = a.IsActive });

                        await _doctorRepository.SaveAsync(profile, desiredAssignments);
                    }
                },
                verificationAsync: async () =>
                {
                    await LoadInitialDataAsync();
                    var idToVerify = isNew ? savedProfileId : profile.Id;
                    return _allDoctorsMasterList.Any(d => d.Id == idToVerify);
                },
                successMessage: isNew ? "Nowy dyżurny został pomyślnie dodany." : "Poprawnie zapisano dane w bazie Supabase.",
                errorMessageTitle: "Błąd zapisu"
            );

            _dispatcher?.TryEnqueue(() =>
            {
                var idToSelect = isNew ? savedProfileId : profile.Id;
                SelectedDoctor = FilteredDoctors.FirstOrDefault(d => d.Id == idToSelect);
            });

            // Wyczyść flagę dirty po pomyślnym zapisie
            ClearDirty();
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
            if (!_allUnits.Any()) return;

            var existingAbbreviations = _allDoctorsMasterList
                .Where(d => d.Id != doctorProfile.Id)
                .Select(d => d.Abbreviation);
            var currentAssignments = await _assignmentRepository.GetForDoctorAsync(doctorProfile.Id);

            EditorViewModel = new DoctorEditorViewModel(
                (DoctorProfile)doctorProfile.Clone(),
                new List<Unit>(_allUnits),
                currentAssignments,
                existingAbbreviations,
                _currentUserId,
                _currentUserLevel
            );
        }

        private async Task ShowInfo(string title, string message)
        {
            var dlg = App.CreateThemedDialog();
            dlg.Title = title;
            dlg.Content = message;
            dlg.PrimaryButtonText = "OK";
            await dlg.ShowAsync();
        }
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
            var dialog = App.CreateThemedDialog();
            dialog.Title = "Potwierdź archiwizację";
            dialog.Content = $"Czy na pewno chcesz zarchiwizować profil lekarza {SelectedDoctor.DisplayName}?";
            dialog.PrimaryButtonText = "Archiwizuj";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Close;

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
            var dialog = App.CreateThemedDialog();
            dialog.Title = "Potwierdź resetowanie hasła";
            dialog.Content = $"Czy na pewno chcesz zresetować hasło dla użytkownika {SelectedDoctor.DisplayName}?";
            dialog.PrimaryButtonText = "Resetuj";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Primary;

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
    }
}