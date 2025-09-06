using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class UnitsSettingsView : UserControl
    {
        private readonly ObservableCollection<Unit> Units = new();
        private DataService? _dataService;

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

            Units.Clear();
            var unitsFromDb = await _dataService.GetAllUnitsAsync();
            foreach (var unit in unitsFromDb.OrderBy(u => u.Name))
            {
                Units.Add(unit);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowUnitDialogAsync(null);
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit)
            {
                await ShowUnitDialogAsync(selectedUnit);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitsListView.SelectedItem is Unit selectedUnit && _dataService != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Potwierdź usunięcie",
                    Content = $"Czy na pewno chcesz usunąć jednostkę '{selectedUnit.Name}'? Tej operacji nie można cofnąć.",
                    PrimaryButtonText = "Usuń",
                    CloseButtonText = "Anuluj",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await _dataService.DeleteUnitAsync(selectedUnit.Id);
                    await LoadUnitsAsync();
                }
            }
        }

        private async Task ShowUnitDialogAsync(Unit? existingUnit)
        {
            if (_dataService == null) return;
            bool isEditMode = existingUnit != null;

            var nameTextBox = new TextBox { Header = "Nazwa skrócona (np. Szpital Miejski)", Text = existingUnit?.Name ?? "" };
            var hospitalNameTextBox = new TextBox { Header = "Pełna nazwa szpitala", Text = existingUnit?.HospitalFullName ?? "" };
            var departmentNameTextBox = new TextBox { Header = "Nazwa oddziału/zakładu", Text = existingUnit?.DepartmentName ?? "" };

            var panel = new StackPanel { Spacing = 12, Children = { nameTextBox, hospitalNameTextBox, departmentNameTextBox } };

            var dialog = new ContentDialog
            {
                Title = isEditMode ? "Edytuj jednostkę" : "Dodaj nową jednostkę",
                Content = panel,
                PrimaryButtonText = "Zapisz",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                // ZMIANA: Ustawiamy stałą, większą szerokość okna
                MinWidth = 700,
                MaxWidth = 700
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var unitToSave = new Unit
                {
                    Id = existingUnit?.Id ?? Guid.NewGuid(),
                    Name = nameTextBox.Text,
                    HospitalFullName = hospitalNameTextBox.Text,
                    DepartmentName = departmentNameTextBox.Text
                };

                await _dataService.SaveUnitAsync(unitToSave);
                await LoadUnitsAsync();
            }
        }
    }
}