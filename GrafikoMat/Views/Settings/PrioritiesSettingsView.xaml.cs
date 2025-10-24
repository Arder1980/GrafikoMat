using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Zarejestruj przycisk Zapisz w ActionButtonsPanel
            var actions = new List<UiAction>
            {
                new UiAction("Zapisz ustawienia", ViewModel.SaveCommand, isPrimary: true)
            };
            ActionsChanged?.Invoke(actions);
        }

        // ZMIANA: Event handler został całkowicie usunięty.
        // Drag & drop jest teraz obsługiwany przez CollectionChanged event w ViewModel.
        // Nie potrzebujemy już ActivePrioritiesListView_DragItemsCompleted.
    }
}