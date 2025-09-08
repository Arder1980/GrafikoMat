using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class UnitsSettingsView : UserControl
    {
        private readonly List<Unit> _masterUnitList = new();
        private readonly ObservableCollection<Unit> DisplayedUnits = new();

        private DataService? _dataService;

        private bool _isAutocompleteActive = true;
        private Unit? _currentSuggestion;

        public UnitsSettingsView()
        {
            this.InitializeComponent();
        }

        public async void Initialize(DataService service)
        {
            _dataService = service;
            await LoadUnitsAsync();
        }

        private async Task LoadUnitsAsync()
        {
            if (_dataService == null) return;

            _masterUnitList.Clear();
            var unitsFromDb = await _dataService.GetAllUnitsAsync();
            _masterUnitList.AddRange(unitsFromDb.OrderBy(u => u.Name));

            FilterUnits();
            UpdateButtonStates();
        }

        private void FilterUnits()
        {
            DisplayedUnits.Clear();
            bool showArchived = ShowArchivedCheckBox.IsChecked ?? false;
            var sourceList = showArchived ? _masterUnitList : _masterUnitList.Where(u => !u.IsArchived);

            foreach (var unit in sourceList)
            {
                DisplayedUnits.Add(unit);
            }
        }

        private void UpdateButtonStates()
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit)
            {
                EditButton.IsEnabled = !selectedUnit.IsArchived;

                if (selectedUnit.IsArchived)
                {
                    ArchiveButton.Visibility = Visibility.Collapsed;
                    RestoreButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ArchiveButton.Visibility = Visibility.Visible;
                    RestoreButton.Visibility = Visibility.Collapsed;
                    ArchiveButton.IsEnabled = true;
                }
            }
            else
            {
                EditButton.IsEnabled = false;
                ArchiveButton.IsEnabled = false;
                ArchiveButton.Visibility = Visibility.Visible;
                RestoreButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowUnitDialogAsync(null);
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit && !selectedUnit.IsArchived)
            {
                await ShowUnitDialogAsync(selectedUnit);
            }
        }

        private async void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit && _dataService != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Potwierdź archiwizację",
                    Content = $"Czy na pewno chcesz zarchiwizować jednostkę '{selectedUnit.Name}'? Zostanie ona ukryta na listach, ale będzie można ją przywrócić.",
                    PrimaryButtonText = "Archiwizuj",
                    CloseButtonText = "Anuluj",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await _dataService.SetUnitArchiveStatusAsync(selectedUnit.Id, true);
                    await LoadUnitsAsync();
                }
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit && _dataService != null)
            {
                await _dataService.SetUnitArchiveStatusAsync(selectedUnit.Id, false);
                await LoadUnitsAsync();
            }
        }

        private async Task ShowUnitDialogAsync(Unit? existingUnit)
        {
            if (_dataService == null) return;
            bool isEditMode = existingUnit != null;
            _currentSuggestion = null;
            _isAutocompleteActive = true;

            var hospitalNameTextBox = new TextBox { Header = "Pełna nazwa szpitala", Text = existingUnit?.HospitalFullName ?? "" };
            var departmentNameTextBox = new TextBox { Header = "Nazwa oddziału/zakładu", Text = existingUnit?.DepartmentName ?? "" };
            var nameTextBox = new TextBox { Header = "Nazwa skrócona (np. Szpital Miejski)", Text = existingUnit?.Name ?? "" };

            hospitalNameTextBox.TextChanged += HospitalNameTextBox_TextChanged;
            hospitalNameTextBox.KeyDown += HospitalNameTextBox_KeyDown;
            hospitalNameTextBox.LostFocus += HospitalNameTextBox_LostFocus;

            var panel = new StackPanel
            {
                Spacing = 12,
                Children = { hospitalNameTextBox, departmentNameTextBox, nameTextBox },
                Width = 650
            };

            var dialog = new ContentDialog
            {
                Title = isEditMode ? "Edytuj jednostkę" : "Dodaj nową jednostkę",
                Content = panel,
                PrimaryButtonText = "Zapisz",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = this.XamlRoot
            };

            hospitalNameTextBox.Tag = new Tuple<TextBox, TextBox>(nameTextBox, departmentNameTextBox);

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var unitToSave = existingUnit ?? new Unit { Id = Guid.NewGuid() };
                unitToSave.Name = nameTextBox.Text;
                unitToSave.HospitalFullName = hospitalNameTextBox.Text;
                unitToSave.DepartmentName = departmentNameTextBox.Text;

                await _dataService.SaveUnitAsync(unitToSave);
                await LoadUnitsAsync();
            }
        }

        // POPRAWIONA METODA: Zmieniono logikę, aby uniknąć błędu kompilatora
        private async void HospitalNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Podstawowe warunki wyjścia
            if (sender is not TextBox hospitalTextBox || string.IsNullOrWhiteSpace(hospitalTextBox.Text) || _dataService == null)
            {
                return;
            }

            // Bezpieczne pobranie powiązanych kontrolek
            if (hospitalTextBox.Tag is not Tuple<TextBox, TextBox> otherBoxes)
            {
                return;
            }

            // Sprawdź, czy skrót jest już wypełniony - jeśli tak, nie rób nic
            if (!string.IsNullOrWhiteSpace(otherBoxes.Item1.Text))
            {
                return;
            }

            // Teraz, gdy wiemy, że `otherBoxes` istnieje, możemy kontynuować
            var match = await _dataService.GetFirstUnitByExactHospitalNameAsync(hospitalTextBox.Text);
            if (match != null)
            {
                otherBoxes.Item1.Text = match.Name;
            }
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

                    if (e.Key == VirtualKey.Tab)
                    {
                        otherBoxes.Item2.Focus(FocusState.Programmatic);
                    }
                }

                _currentSuggestion = null;
                _isAutocompleteActive = true;
            }
        }

        private async void HospitalNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isAutocompleteActive || _dataService == null || sender is not TextBox hospitalTextBox) return;

            var userText = hospitalTextBox.Text;
            var selectionStart = hospitalTextBox.SelectionStart;

            if (selectionStart < userText.Length)
            {
                _currentSuggestion = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(userText) || userText.Length < 3)
            {
                _currentSuggestion = null;
                return;
            }

            var match = await _dataService.GetUniqueUnitByHospitalNameStartAsync(userText);
            _currentSuggestion = match;

            if (match != null && match.HospitalFullName.Length > userText.Length)
            {
                _isAutocompleteActive = false;

                hospitalTextBox.Text = match.HospitalFullName;
                hospitalTextBox.Select(userText.Length, match.HospitalFullName.Length - userText.Length);

                _isAutocompleteActive = true;
            }
        }

        private void ShowArchivedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            FilterUnits();
            UpdateButtonStates();
        }

        private void UnitsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }
    }
}