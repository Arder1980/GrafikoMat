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

        public ObservableCollection<DoctorProfile> AllDoctors { get; } = new();
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
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isStatusMessageOpen;
        public bool IsStatusMessageOpen
        {
            get => _isStatusMessageOpen;
            set => SetProperty(ref _isStatusMessageOpen, value);
        }

        private string _statusMessageTitle = string.Empty;
        public string StatusMessageTitle
        {
            get => _statusMessageTitle;
            set => SetProperty(ref _statusMessageTitle, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private InfoBarSeverity _statusMessageSeverity;
        public InfoBarSeverity StatusMessageSeverity
        {
            get => _statusMessageSeverity;
            set => SetProperty(ref _statusMessageSeverity, value);
        }

        public RelayCommand AddNewDoctorCommand { get; }
        public AsyncRelayCommand SaveDoctorCommand { get; }
        public AsyncRelayCommand ResetPasswordCommand { get; }

        public ManagementViewModel(DataService? dataService, DispatcherQueue? dispatcher)
        {
            _dataService = dataService;
            _dispatcher = dispatcher;

            AddNewDoctorCommand = new RelayCommand(AddNewDoctor);
            SaveDoctorCommand = new AsyncRelayCommand(SaveDoctor, () => EditorViewModel?.IsValid ?? false);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPassword, () => IsDoctorSelectedAndNotNew);

            if (_dataService != null)
            {
                Task.Run(LoadInitialDataAsync);
            }
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorViewModel.IsValid))
            {
                SaveDoctorCommand.NotifyCanExecuteChanged();
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
                    AllDoctors.Clear();
                    foreach (var doctor in doctors.OrderBy(d => d.LastName))
                    {
                        AllDoctors.Add(doctor);
                    }

                    if (previouslySelectedId != null)
                    {
                        SelectedDoctor = AllDoctors.FirstOrDefault(d => d.Id == previouslySelectedId);
                    }
                });
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd ładowania danych", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                // ZMIANA: Używamy Dispatchera, aby mieć pewność, że zmiana nastąpi w wątku UI
                _dispatcher?.TryEnqueue(() => IsLoading = false);
            }
        }

        private void AddNewDoctor()
        {
            SelectedDoctor = null;
            EditorViewModel = new DoctorEditorViewModel(new DoctorProfile { Id = Guid.Empty }, _allUnits, new List<UnitDoctorAssignment>(), AllDoctors.Select(d => d.Abbreviation));
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

                // Przywracamy zaznaczenie
                _dispatcher?.TryEnqueue(() =>
                {
                    SelectedDoctor = AllDoctors.FirstOrDefault(d => d.Id == savedProfileId);
                });

                ShowStatusMessage("Sukces!", "Dane zostały pomyślnie zapisane.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd zapisu", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                // ZMIANA: Używamy Dispatchera
                _dispatcher?.TryEnqueue(() => IsLoading = false);
            }
        }

        private async Task ResetPassword()
        {
            if (SelectedDoctor == null || EditorViewModel == null) return;

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
                // Logika resetowania hasła...
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
                var existingAbbreviations = AllDoctors
                    .Where(d => d.Id != doctorProfile.Id)
                    .Select(d => d.Abbreviation);

                var currentAssignments = await _dataService.GetAssignmentsForDoctorAsync(doctorProfile.Id);
                EditorViewModel = new DoctorEditorViewModel((DoctorProfile)doctorProfile.Clone(), _allUnits, currentAssignments, existingAbbreviations);
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Błąd ładowania edytora", $"Wystąpił błąd: {ex.Message}", InfoBarSeverity.Error);
                EditorViewModel = null;
            }
            finally
            {
                // ZMIANA: Używamy Dispatchera
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

        private void HideStatusMessage()
        {
            if (_dispatcher?.HasThreadAccess == false)
            {
                _dispatcher.TryEnqueue(() => IsStatusMessageOpen = false);
            }
            else
            {
                IsStatusMessageOpen = false;
            }
        }
    }
}