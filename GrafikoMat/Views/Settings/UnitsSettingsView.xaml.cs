using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Models;
using CommunityToolkit.Mvvm.Input;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class UnitsSettingsView : UserControl, INotifyPropertyChanged
    {
        private readonly List<Unit> _masterUnitList = new();
        private readonly ObservableCollection<Unit> DisplayedUnits = new();

        private readonly IUnitRepository? _unitRepository;
        private readonly IUxActionOrchestrator _orchestrator;

        private bool _isAutocompleteActive = true;
        private Unit? _currentSuggestion;
        private string _searchText = string.Empty;
        private bool _isFormVisible = false;

        public event Action<List<UiAction>>? ActionsChanged;

        private Unit? _selectedUnit;
        public Unit? SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (_selectedUnit != value)
                {
                    // Jeśli zmieniamy z jednej jednostki na inną
                    if (_isFormVisible && value != null)
                    {
                        // Fade out, potem zmień dane, potem fade in
                        FormFadeOutStoryboard.Completed -= OnFadeOutForUnitChange;
                        FormFadeOutStoryboard.Completed += OnFadeOutForUnitChange;
                        _pendingSelectedUnit = value;
                        FormFadeOutStoryboard.Begin();
                    }
                    else
                    {
                        _selectedUnit = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(IsEditMode));
                        OnPropertyChanged(nameof(IsArchived));
                        OnPropertyChanged(nameof(EditModeTitle));
                        OnPropertyChanged(nameof(ShouldShowArchiveButton));
                        BuildActions();

                        if (value != null)
                        {
                            // Pierwsze otwarcie - fade in
                            _isFormVisible = true;
                            EditFormGrid.Opacity = 0;
                            _ = Task.Delay(50).ContinueWith(_ =>
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    // Ustaw Tag dla autouzupełniania
                                    HospitalNameTextBox.Tag = new Tuple<TextBox, TextBox>(NameTextBox, DepartmentNameTextBox);
                                    FormFadeInStoryboard.Begin();
                                });
                            });
                        }
                        else
                        {
                            // Zamknięcie
                            _isFormVisible = false;
                            EditFormGrid.Opacity = 0;
                        }
                    }
                }
            }
        }

        private Unit? _pendingSelectedUnit;

        private void OnFadeOutForUnitChange(object? sender, object e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutForUnitChange;

            _selectedUnit = _pendingSelectedUnit;
            _pendingSelectedUnit = null;

            OnPropertyChanged(nameof(SelectedUnit));
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(IsArchived));
            OnPropertyChanged(nameof(EditModeTitle));
            OnPropertyChanged(nameof(ShouldShowArchiveButton));
            BuildActions();

            // Ustaw Tag dla autouzupełniania
            HospitalNameTextBox.Tag = new Tuple<TextBox, TextBox>(NameTextBox, DepartmentNameTextBox);
            FormFadeInStoryboard.Begin();
        }

        public bool IsEditMode => SelectedUnit != null && _masterUnitList.Any(u => u.Id == SelectedUnit.Id);
        public bool IsArchived => SelectedUnit?.IsArchived ?? false;
        public string EditModeTitle => IsEditMode ? "Edytuj jednostkę" : "Nowa jednostka";
        public bool ShouldShowArchiveButton => IsEditMode && !IsArchived;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UnitsSettingsView(IUnitRepository unitRepository)
        {
            this.InitializeComponent();
            _unitRepository = unitRepository;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            this.Loaded += UnitsSettingsView_Loaded;
            BuildActions();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void UnitsSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            await _orchestrator.PerformLoadAsync(
                viewId: ActionContainer.GetViewId(),
                loadActionAsync: LoadUnitsAsync
            );
        }

        private async Task LoadUnitsAsync()
        {
            if (_unitRepository == null) return;
            _masterUnitList.Clear();
            var unitsFromDb = await _unitRepository.GetAllAsync();
            _masterUnitList.AddRange(unitsFromDb.OrderBy(u => u.Name));

            DispatcherQueue.TryEnqueue(() =>
            {
                FilterUnits();
                MainContentGrid.Visibility = Visibility.Visible;
            });
        }

        private void FilterUnits()
        {
            DisplayedUnits.Clear();
            bool showArchived = ShowArchivedCheckBox.IsChecked ?? false;
            var sourceList = showArchived ? _masterUnitList : _masterUnitList.Where(u => !u.IsArchived);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                sourceList = sourceList.Where(u =>
                    u.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    u.DepartmentName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    u.HospitalFullName.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var unit in sourceList)
            {
                DisplayedUnits.Add(unit);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchBox.Text;
            FilterUnits();
        }

        private void ShowArchivedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            FilterUnits();
        }

        private void UnitsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit)
            {
                SelectedUnit = new Unit
                {
                    Id = selectedUnit.Id,
                    Name = selectedUnit.Name,
                    HospitalFullName = selectedUnit.HospitalFullName,
                    DepartmentName = selectedUnit.DepartmentName,
                    UseTwelveHourShiftsByDefault = selectedUnit.UseTwelveHourShiftsByDefault,
                    AllowTeleradiologyFallback = selectedUnit.AllowTeleradiologyFallback,
                    IsArchived = selectedUnit.IsArchived
                };

                HospitalNameTextBox.Tag = new Tuple<TextBox, TextBox>(NameTextBox, DepartmentNameTextBox);
            }
        }

        private void AddNewButton_Click(object sender, RoutedEventArgs e)
        {
            UnitsListView.SelectedItem = null;
            SelectedUnit = new Unit
            {
                Id = Guid.NewGuid(),
                Name = string.Empty,
                HospitalFullName = string.Empty,
                DepartmentName = string.Empty,
                UseTwelveHourShiftsByDefault = false,
                AllowTeleradiologyFallback = false,
                IsArchived = false
            };

            // Tag zostanie ustawiony w SelectedUnit setter po opóźnieniu
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs? e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutForCancel;
            FormFadeOutStoryboard.Completed += OnFadeOutForCancel;
            FormFadeOutStoryboard.Begin();
        }

        private void OnFadeOutForCancel(object? sender, object e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutForCancel;
            UnitsListView.SelectedItem = null;
            SelectedUnit = null;
            _isFormVisible = false;
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (SelectedUnit == null || _unitRepository == null) return;

            if (string.IsNullOrWhiteSpace(SelectedUnit.Name) ||
                string.IsNullOrWhiteSpace(SelectedUnit.HospitalFullName) ||
                string.IsNullOrWhiteSpace(SelectedUnit.DepartmentName))
            {
                var dialog = App.CreateThemedDialog();
                dialog.Title = "Niepełne dane";
                dialog.Content = "Wszystkie pola są wymagane.";
                dialog.CloseButtonText = "OK";
                await dialog.ShowAsync();
                return;
            }

            var unitToSave = new Unit
            {
                Id = SelectedUnit.Id,
                Name = SelectedUnit.Name,
                HospitalFullName = SelectedUnit.HospitalFullName,
                DepartmentName = SelectedUnit.DepartmentName,
                UseTwelveHourShiftsByDefault = SelectedUnit.UseTwelveHourShiftsByDefault,
                AllowTeleradiologyFallback = SelectedUnit.AllowTeleradiologyFallback,
                IsArchived = SelectedUnit.IsArchived
            };

            bool isEditMode = IsEditMode;

            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SaveAsync(unitToSave),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    return _masterUnitList.Any(u => u.Id == unitToSave.Id &&
                                                    u.Name == unitToSave.Name &&
                                                    u.AllowTeleradiologyFallback == unitToSave.AllowTeleradiologyFallback);
                },
                successMessage: isEditMode ? "Poprawnie zapisano zmiany w jednostce." : "Nowa jednostka została pomyślnie dodana.",
                errorMessageTitle: "Błąd zapisu jednostki"
            );

            WeakReferenceMessenger.Default.Send(new UnitDataChangedMessage());

            FormFadeOutStoryboard.Completed -= OnFadeOutAfterSave;
            FormFadeOutStoryboard.Completed += OnFadeOutAfterSave;
            FormFadeOutStoryboard.Begin();
        }

        private void OnFadeOutAfterSave(object? sender, object e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutAfterSave;
            SelectedUnit = null;
            UnitsListView.SelectedItem = null;
            _isFormVisible = false;
        }

        private async void ArchiveButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (SelectedUnit == null || _unitRepository == null) return;

            var dialog = App.CreateThemedDialog();
            dialog.Title = "Potwierdź deaktywację";
            dialog.Content = $"Czy na pewno chcesz deaktywować jednostkę '{SelectedUnit.Name}'? Będzie można ją ponownie aktywować w przyszłości.";
            dialog.PrimaryButtonText = "Deaktywuj";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Close;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var unitId = SelectedUnit.Id;
            var unitName = SelectedUnit.Name;

            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SetArchiveStatusAsync(unitId, true),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    var reloaded = _masterUnitList.FirstOrDefault(u => u.Id == unitId);
                    return reloaded?.IsArchived ?? true;
                },
                successMessage: $"Jednostka '{unitName}' została deaktywowana.",
                errorMessageTitle: "Błąd deaktywacji"
            );

            WeakReferenceMessenger.Default.Send(new UnitDataChangedMessage());

            FormFadeOutStoryboard.Completed -= OnFadeOutAfterArchive;
            FormFadeOutStoryboard.Completed += OnFadeOutAfterArchive;
            FormFadeOutStoryboard.Begin();
        }

        private void OnFadeOutAfterArchive(object? sender, object e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutAfterArchive;
            SelectedUnit = null;
            UnitsListView.SelectedItem = null;
            _isFormVisible = false;
        }

        private async void RestoreButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (SelectedUnit == null || _unitRepository == null) return;

            var unitId = SelectedUnit.Id;
            var unitName = SelectedUnit.Name;

            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SetArchiveStatusAsync(unitId, false),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    var reloaded = _masterUnitList.FirstOrDefault(u => u.Id == unitId);
                    return reloaded != null && !reloaded.IsArchived;
                },
                successMessage: $"Jednostka '{unitName}' została aktywowana ponownie.",
                errorMessageTitle: "Błąd aktywacji"
            );

            WeakReferenceMessenger.Default.Send(new UnitDataChangedMessage());

            FormFadeOutStoryboard.Completed -= OnFadeOutAfterRestore;
            FormFadeOutStoryboard.Completed += OnFadeOutAfterRestore;
            FormFadeOutStoryboard.Begin();
        }

        private void OnFadeOutAfterRestore(object? sender, object e)
        {
            FormFadeOutStoryboard.Completed -= OnFadeOutAfterRestore;
            SelectedUnit = null;
            UnitsListView.SelectedItem = null;
            _isFormVisible = false;
        }

        private async void HospitalNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox hospitalTextBox || string.IsNullOrWhiteSpace(hospitalTextBox.Text) || _unitRepository == null) return;
            if (hospitalTextBox.Tag is not Tuple<TextBox, TextBox> otherBoxes) return;
            if (!string.IsNullOrWhiteSpace(otherBoxes.Item1.Text)) return;

            var match = await _unitRepository.GetFirstByExactHospitalNameAsync(hospitalTextBox.Text);
            if (match != null) otherBoxes.Item1.Text = match.Name;
        }

        private void HospitalNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox hospitalTextBox || _currentSuggestion == null) return;
            if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab)
            {
                e.Handled = true;
                _isAutocompleteActive = false;
                hospitalTextBox.Text = _currentSuggestion.HospitalFullName;
                hospitalTextBox.Select(hospitalTextBox.Text.Length, 0);
                if (hospitalTextBox.Tag is Tuple<TextBox, TextBox> otherBoxes)
                {
                    otherBoxes.Item1.Text = _currentSuggestion.Name;
                    if (e.Key == VirtualKey.Tab) otherBoxes.Item2.Focus(FocusState.Programmatic);
                }
                _currentSuggestion = null;
                _isAutocompleteActive = true;
            }
        }

        private async void HospitalNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isAutocompleteActive || _unitRepository == null || sender is not TextBox hospitalTextBox) return;

            var userText = hospitalTextBox.Text;
            var selectionStart = hospitalTextBox.SelectionStart;

            // Jeśli kursor nie jest na końcu tekstu, nie autouzupełniaj
            if (selectionStart < userText.Length)
            {
                _currentSuggestion = null;
                return;
            }

            // Jeśli tekst jest za krótki, nie autouzupełniaj
            if (string.IsNullOrWhiteSpace(userText) || userText.Length < 3)
            {
                _currentSuggestion = null;
                return;
            }

            // Szukaj unikalnej jednostki pasującej do wpisanego tekstu
            var match = await _unitRepository.GetUniqueByHospitalNameStartAsync(userText);
            _currentSuggestion = match;

            if (match != null && match.HospitalFullName.Length > userText.Length)
            {
                _isAutocompleteActive = false;
                hospitalTextBox.Text = match.HospitalFullName;
                hospitalTextBox.Select(userText.Length, match.HospitalFullName.Length - userText.Length);
                _isAutocompleteActive = true;
            }
        }

        private void BuildActions()
        {
            var actions = new List<UiAction>();

            if (SelectedUnit != null)
            {
                actions.Add(new UiAction("Anuluj", new RelayCommand(() => CancelButton_Click(null, null)), isPrimary: false));

                if (ShouldShowArchiveButton)
                {
                    actions.Add(new UiAction("Deaktywuj", new RelayCommand(() => ArchiveButton_Click(null, null)), isPrimary: false));
                }

                if (IsArchived)
                {
                    actions.Add(new UiAction("Aktywuj ponownie", new RelayCommand(() => RestoreButton_Click(null, null)), isPrimary: false));
                }

                actions.Add(new UiAction("Zapisz zmiany", new RelayCommand(execute: () => SaveButton_Click(null, null), canExecute: () => !IsArchived), isPrimary: true));
            }

            ActionsChanged?.Invoke(actions);
        }
    }
}