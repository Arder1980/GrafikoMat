using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GrafikoMat.ViewModels
{
    public class ManagementViewModel : ObservableObject
    {
        private readonly DataService? _dataService;
        private readonly DispatcherQueue? _dispatcherQueue;

        // ZMIANA: Flaga do ostatecznego przerwania pętli rekurencji.
        private bool _isSelectionChanging = false;

        public ObservableCollection<DoctorProfile> AllDoctors { get; } = new();
        private List<Unit> _allUnits = new();

        private DoctorProfile? _selectedDoctor;
        public DoctorProfile? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                // Jeśli jesteśmy w trakcie zmiany, ignorujemy wszystkie przychodzące wywołania.
                if (_isSelectionChanging) return;

                // Sprawdzamy, czy nowa wartość faktycznie się różni od starej, aby uniknąć zbędnej pracy.
                if (object.ReferenceEquals(_selectedDoctor, value)) return;

                _isSelectionChanging = true;

                // Używamy SetProperty do aktualizacji pola i powiadomienia UI.
                SetProperty(ref _selectedDoctor, value);

                if (_selectedDoctor != null)
                {
                    // Jeśli wybrano istniejącego lekarza, ładujemy jego dane.
                    LoadEditorFor(_selectedDoctor);
                }
                else
                {
                    // Jeśli odznaczono lekarza, upewniamy się, że edytor jest pusty,
                    // chyba że jest to edytor dla nowego lekarza.
                    if (EditorViewModel != null && !EditorViewModel.IsNewDoctor)
                    {
                        EditorViewModel = null;
                    }
                }

                _isSelectionChanging = false;
            }
        }

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
                    SaveDoctorCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddNewDoctorCommand { get; }
        public RelayCommand SaveDoctorCommand { get; }

        public ManagementViewModel(DataService? dataService, DispatcherQueue? dispatcher)
        {
            _dataService = dataService;
            _dispatcherQueue = dispatcher;

            AddNewDoctorCommand = new RelayCommand(AddNewDoctor);
            SaveDoctorCommand = new RelayCommand(
                execute: _ => { _ = SaveDoctorAsync(); },
                canExecute: _ => EditorViewModel?.IsValid ?? false);

            if (_dataService != null)
            {
                Task.Run(LoadInitialDataAsync);
            }
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SaveDoctorCommand.RaiseCanExecuteChanged();
        }

        private async Task LoadInitialDataAsync()
        {
            if (_dataService == null) return;

            var previouslySelectedId = SelectedDoctor?.Id;

            _dispatcherQueue?.TryEnqueue(() =>
            {
                AllDoctors.Clear();
            });

            var doctors = await _dataService.GetAllDoctorsAsync();
            var units = await _dataService.GetAllUnitsAsync();

            _allUnits = units;

            _dispatcherQueue?.TryEnqueue(() =>
            {
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

        private async void LoadEditorFor(DoctorProfile doctorProfile)
        {
            if (_dataService == null) return;

            var existingAbbreviations = AllDoctors
                .Where(d => d.Id != doctorProfile.Id)
                .Select(d => d.Abbreviation);

            var currentAssignments = await _dataService.GetAssignmentsForDoctorAsync(doctorProfile.Id);

            EditorViewModel = new DoctorEditorViewModel(doctorProfile, _allUnits, currentAssignments, existingAbbreviations);
        }

        private void AddNewDoctor(object? obj)
        {
            // ZMIANA: Uproszczona i bezpieczna logika.
            // Najpierw odznaczamy cokolwiek jest na liście. Setter SelectedDoctor
            // bezpiecznie wyczyści edytor.
            SelectedDoctor = null;

            // Dopiero teraz tworzymy edytor dla nowego użytkownika.
            var newProfile = new DoctorProfile { Id = Guid.Empty };
            var existingAbbreviations = AllDoctors.Select(d => d.Abbreviation);
            EditorViewModel = new DoctorEditorViewModel(newProfile, _allUnits, new List<UnitDoctorAssignment>(), existingAbbreviations);
        }

        private async Task SaveDoctorAsync()
        {
            if (_dataService == null || EditorViewModel == null) return;
            if (!EditorViewModel.IsValid) return;

            await _dataService.SaveDoctorAsync(EditorViewModel);
            await LoadInitialDataAsync();
        }
    }
}