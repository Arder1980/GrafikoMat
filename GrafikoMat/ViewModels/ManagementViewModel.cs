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

        // ZMIANA: Ta kolekcja będzie teraz przechowywać pełną, niefiltrowaną listę lekarzy
        private readonly List<DoctorProfile> _allDoctorsMasterList = new();

        // ZMIANA: Nowa kolekcja, powiązana z UI, przechowująca odfiltrowane wyniki
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
                }
            }
        }

        // ZMIANA: Nowa właściwość dla pola wyszukiwania
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
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private bool _isStatusMessageOpen;
        public bool IsStatusMessageOpen { get => _isStatusMessageOpen; set => SetProperty(ref _isStatusMessageOpen, value); }

        private string _statusMessageTitle = string.Empty;
        public string StatusMessageTitle { get => _statusMessageTitle; set => SetProperty(ref _statusMessageTitle, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private InfoBarSeverity _statusMessageSeverity;
        public InfoBarSeverity StatusMessageSeverity { get => _statusMessageSeverity; set => SetProperty(ref _statusMessageSeverity, value); }

        public AsyncRelayCommand AddNewDoctorCommand { get; }
        public AsyncRelayCommand SaveDoctorCommand { get; }
        public AsyncRelayCommand ResetPasswordCommand { get; }

        public ManagementViewModel(DataService? dataService, DispatcherQueue? dispatcher)
        {
            _dataService = dataService;
            _dispatcher = dispatcher;

            AddNewDoctorCommand = new AsyncRelayCommand(AddNewDoctorAsync);
            SaveDoctorCommand = new AsyncRelayCommand(SaveDoctor, () => EditorViewModel?.IsValid ?? false);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPassword, () => IsDoctorSelectedAndNotNew && (EditorViewModel?.ShowResetButton ?? false));
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
            _dispatcher?.TryEnqueue(() => IsLoading = true);
            try
            {
                _allUnits = await _dataService.GetAllUnitsAsync();
            }
            finally
            {
                _dispatcher?.TryEnqueue(() => IsLoading = false);
            }
        }

        private async Task LoadInitialDataAsync()
        {
            if (_dataService == null) return;
            var previouslySelectedId = SelectedDoctor?.Id;

            _dispatcher?.TryEnqueue(() => IsLoading = true);
            try
            {
                var doctors = await _dataService.GetAllDoctorsAsync();
                _allUnits = await _dataService.GetAllUnitsAsync();

                _dispatcher?.TryEnqueue(() =>
                {
                    _allDoctorsMasterList.Clear();
                    _allDoctorsMasterList.AddRange(doctors.OrderBy(d => d.LastName));

                    FilterDoctors(); // ZMIANA: Zamiast ładować do AllDoctors, filtrujemy

                    if (previouslySelectedId != null)
                    {
                        SelectedDoctor = _allDoctorsMasterList.FirstOrDefault(d => d.Id == previouslySelectedId);
                    }
                });
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd ładowania danych", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                _dispatcher?.TryEnqueue(() => IsLoading = false);
            }
        }

        // ZMIANA: Nowa metoda filtrująca
        private void FilterDoctors()
        {
            FilteredDoctors.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var doctor in _allDoctorsMasterList)
                {
                    FilteredDoctors.Add(doctor);
                }
            }
            else
            {
                var searchTextLower = SearchText.ToLowerInvariant();
                var filtered = _allDoctorsMasterList.Where(d =>
                    d.LastName.ToLowerInvariant().Contains(searchTextLower) ||
                    d.FirstName.ToLowerInvariant().Contains(searchTextLower));

                foreach (var doctor in filtered)
                {
                    FilteredDoctors.Add(doctor);
                }
            }
        }

        private async Task AddNewDoctorAsync()
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

        private async Task SaveDoctor()
        {
            if (_dataService == null || EditorViewModel == null || !EditorViewModel.IsValid) return;
            IsLoading = true;
            HideStatusMessage();

            try
            {
                var savedProfileId = EditorViewModel.Profile.Id;
                await _dataService.SaveDoctorAsync(EditorViewModel);
                await LoadInitialDataAsync();

                _dispatcher?.TryEnqueue(() =>
                {
                    SelectedDoctor = _allDoctorsMasterList.FirstOrDefault(d => d.Id == savedProfileId);
                });
                ShowStatusMessage("Sukces!", "Dane zostały pomyślnie zapisane.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd zapisu", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                _dispatcher?.TryEnqueue(() => IsLoading = false);
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
                XamlRoot = App.MainWindow.Content.XamlRoot
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
                    ShowStatusMessage("Sukces!", $"Hasło zostało zresetowane. Nowe hasło startowe: {newPassword}", InfoBarSeverity.Success);
                }
                catch (Exception ex)
                {
                    ShowStatusMessage("Błąd resetowania hasła", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
                }
                finally
                {
                    _dispatcher?.TryEnqueue(() => IsLoading = false);
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
                _dispatcher?.TryEnqueue(() => IsLoading = false);
            }
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