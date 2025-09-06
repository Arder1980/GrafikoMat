using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class ManagementViewModel : ObservableObject
    {
        private readonly DataService? _dataService;

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
                    if (_selectedDoctor != null)
                    {
                        LoadEditorFor(_selectedDoctor);
                    }
                    else
                    {
                        EditorViewModel = null;
                    }
                }
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

        public ManagementViewModel(DataService? dataService)
        {
            _dataService = dataService;

            AddNewDoctorCommand = new RelayCommand(AddNewDoctor);
            SaveDoctorCommand = new RelayCommand(
                execute: _ => { _ = SaveDoctorAsync(); },
                canExecute: _ => EditorViewModel?.IsValid ?? false);

            LoadInitialDataAsync();
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Gdy jakakolwiek właściwość w edytorze się zmieni,
            // każ przyciskowi "Zapisz" ponownie sprawdzić, czy jego warunki są spełnione.
            SaveDoctorCommand.RaiseCanExecuteChanged();
        }

        private async void LoadInitialDataAsync()
        {
            if (_dataService == null) return;

            AllDoctors.Clear();
            _allUnits.Clear();

            var doctors = await _dataService.GetAllDoctorsAsync();
            var units = await _dataService.GetAllUnitsAsync();

            _allUnits = units;
            foreach (var doctor in doctors.OrderBy(d => d.LastName))
            {
                AllDoctors.Add(doctor);
            }
        }

        private async void LoadEditorFor(DoctorProfile doctorProfile)
        {
            if (_dataService == null) return;
            var currentAssignments = await _dataService.GetAssignmentsForDoctorAsync(doctorProfile.Id);
            EditorViewModel = new DoctorEditorViewModel(doctorProfile, _allUnits, currentAssignments);
        }

        private void AddNewDoctor(object? obj)
        {
            var newProfile = new DoctorProfile { Id = Guid.Empty };
            EditorViewModel = new DoctorEditorViewModel(newProfile, _allUnits, new List<UnitDoctorAssignment>());
            SelectedDoctor = null;
        }

        private async Task SaveDoctorAsync()
        {
            if (_dataService == null || EditorViewModel == null) return;
            if (!EditorViewModel.IsValid) return;

            await _dataService.SaveDoctorAsync(EditorViewModel);

            LoadInitialDataAsync();
            EditorViewModel = null;
        }
    }
}