using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class PrioritiesSettingsView : UserControl
    {
        public PrioritiesSettingsViewModel ViewModel { get; }
        public event Action<List<UiAction>>? ActionsChanged;

        public PrioritiesSettingsView(PrioritiesSettingsViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Najpierw wyślij pustą listę aby wyczyścić panel akcji
            ActionsChanged?.Invoke(new List<UiAction>());

            // Opóźnij rejestrację akcji aby uniknąć migania przycisku podczas inicjalizacji
            await Task.Delay(100);

            if (this.IsLoaded)
            {
                var actions = new List<UiAction>
                {
                    new UiAction("Zapisz zmiany", ViewModel.SaveCommand, isPrimary: true)
                };
                ActionsChanged?.Invoke(actions);
            }
        }

        // ZMIANA: Event handler został całkowicie usunięty.
        // Drag & drop jest teraz obsługiwany przez CollectionChanged event w ViewModel.
        // Nie potrzebujemy już ActivePrioritiesListView_DragItemsCompleted.
    }
}