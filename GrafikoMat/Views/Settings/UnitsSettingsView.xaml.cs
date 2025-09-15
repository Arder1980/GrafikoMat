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
// NOWE USINGI
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Models;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class UnitsSettingsView : UserControl
    {
        private readonly List<Unit> _masterUnitList = new();
        private readonly ObservableCollection<Unit> DisplayedUnits = new();

        private readonly IUnitRepository? _unitRepository;
        private readonly IUxActionOrchestrator _orchestrator;

        private bool _isAutocompleteActive = true;
        private Unit? _currentSuggestion;

        public UnitsSettingsView(IUnitRepository unitRepository)
        {
            this.InitializeComponent();
            _unitRepository = unitRepository;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            this.Loaded += UnitsSettingsView_Loaded;
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

            DispatcherQueue.TryEnqueue(() => {
                FilterUnits();
                UpdateButtonStates();
            });
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
            if (selectionStart < userText.Length) { _currentSuggestion = null; return; }
            if (string.IsNullOrWhiteSpace(userText) || userText.Length < 3) { _currentSuggestion = null; return; }
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

        private void ShowArchivedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            FilterUnits();
            UpdateButtonStates();
        }

        private void UnitsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtonStates();

        private async void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is not Unit selectedUnit || _unitRepository == null) return;
            var dialog = App.CreateThemedDialog();
            dialog.Title = "Potwierdź archiwizację";
            dialog.Content = $"Czy na pewno chcesz zarchiwizować jednostkę '{selectedUnit.Name}'?";
            dialog.PrimaryButtonText = "Archiwizuj";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Close;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SetArchiveStatusAsync(selectedUnit.Id, true),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    var reloaded = _masterUnitList.FirstOrDefault(u => u.Id == selectedUnit.Id);
                    return reloaded?.IsArchived ?? true;
                },
                successMessage: $"Jednostka '{selectedUnit.Name}' została zarchiwizowana.",
                errorMessageTitle: "Błąd archiwizacji"
            );
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is not Unit selectedUnit || _unitRepository == null) return;
            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SetArchiveStatusAsync(selectedUnit.Id, false),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    var reloaded = _masterUnitList.FirstOrDefault(u => u.Id == selectedUnit.Id);
                    return reloaded != null && !reloaded.IsArchived;
                },
                successMessage: $"Jednostka '{selectedUnit.Name}' została przywrócona.",
                errorMessageTitle: "Błąd przywracania"
            );
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e) => await ShowUnitDialogAsync(null);
        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit) await ShowUnitDialogAsync(selectedUnit);
        }

        private async Task ShowUnitDialogAsync(Unit? existingUnit)
        {
            if (_unitRepository == null) return;
            bool isEditMode = existingUnit != null;
            _currentSuggestion = null;
            _isAutocompleteActive = true;
            var hospitalNameTextBox = new TextBox { Header = "Pełna nazwa szpitala", Text = existingUnit?.HospitalFullName ?? "" };
            var departmentNameTextBox = new TextBox { Header = "Nazwa oddziału/zakładu", Text = existingUnit?.DepartmentName ?? "" };
            var nameTextBox = new TextBox { Header = "Nazwa skrócona (np. Szpital Miejski)", Text = existingUnit?.Name ?? "" };
            var use12hCheckBox = new CheckBox
            {
                Content = "Używaj domyślnie dyżurów 12-godzinnych (Dzień/Noc)",
                IsChecked = existingUnit?.UseTwelveHourShiftsByDefault ?? false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            hospitalNameTextBox.TextChanged += HospitalNameTextBox_TextChanged;
            hospitalNameTextBox.KeyDown += HospitalNameTextBox_KeyDown;
            hospitalNameTextBox.LostFocus += HospitalNameTextBox_LostFocus;
            hospitalNameTextBox.Tag = new Tuple<TextBox, TextBox>(nameTextBox, departmentNameTextBox);
            var panel = new StackPanel { Spacing = 12, Children = { hospitalNameTextBox, departmentNameTextBox, nameTextBox, use12hCheckBox }, Width = 650 };
            var dialog = App.CreateThemedDialog();
            dialog.Title = isEditMode ? "Edytuj jednostkę" : "Dodaj nową jednostkę";
            dialog.Content = panel;
            dialog.PrimaryButtonText = "Zapisz";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Primary;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var unitToSave = existingUnit ?? new Unit { Id = Guid.NewGuid() };
            unitToSave.Name = nameTextBox.Text;
            unitToSave.HospitalFullName = hospitalNameTextBox.Text;
            unitToSave.DepartmentName = departmentNameTextBox.Text;
            unitToSave.UseTwelveHourShiftsByDefault = use12hCheckBox.IsChecked ?? false;

            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => await _unitRepository.SaveAsync(unitToSave),
                verificationAsync: async () =>
                {
                    await LoadUnitsAsync();
                    return _masterUnitList.Any(u => u.Id == unitToSave.Id && u.Name == unitToSave.Name);
                },
                successMessage: isEditMode ? "Poprawnie zapisano zmiany w jednostce." : "Nowa jednostka została pomyślnie dodana.",
                errorMessageTitle: "Błąd zapisu jednostki"
            );

            // ================== NOWA LINIA ==================
            // Informujemy resztę aplikacji, że dane jednostek mogły się zmienić.
            WeakReferenceMessenger.Default.Send(new UnitDataChangedMessage());
            // ==============================================
        }
    }
}