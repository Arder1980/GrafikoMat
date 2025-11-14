using GrafikoMat.Core.Data;
using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class CalendarSettingsView : UserControl
    {
        public CalendarSettingsViewModel ViewModel { get; }
        public event Action<List<UiAction>>? ActionsChanged;

        public CalendarSettingsView(CalendarSettingsViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            ViewModel.RequestAddCustomDay += ShowAddCustomDayDialogAsync;
            this.Loaded += OnLoaded;

            // Ustaw polską kulturę dla kalendarza
            WinterStartDatePicker.Language = "pl-PL";
            WinterStartDatePicker.FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Najpierw wyślij pustą listę aby wyczyścić panel akcji
            ActionsChanged?.Invoke(new List<UiAction>());

            // Załaduj jednostki i dane
            await ViewModel.LoadUnitsAsync();
            await ViewModel.LoadDataAsync();

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

        private async Task ShowAddCustomDayDialogAsync()
        {
            // Pola dialogu
            var nameTextBox = new TextBox
            {
                PlaceholderText = "Np. 'Koniec r. szk.'",
                MaxLength = 15,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var fullNameTextBox = new TextBox
            {
                PlaceholderText = "Np. 'Koniec roku szkolnego'",
                MaxLength = 200,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var datePicker = new CalendarDatePicker
            {
                PlaceholderText = "Wybierz datę",
                Language = "pl-PL",
                FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
                DateFormat = "{day.integer}.{month.integer}.{year.full}",
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Krótka nazwa (max 15 znaków):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    nameTextBox,
                    new TextBlock { Text = "Pełna nazwa:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) },
                    fullNameTextBox,
                    new TextBlock { Text = "Data:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) },
                    datePicker
                }
            };

            var dialog = App.CreateThemedDialog();
            dialog.Title = "Dodaj dzień specjalny";
            dialog.Content = stackPanel;
            dialog.PrimaryButtonText = "Dodaj";
            dialog.CloseButtonText = "Anuluj";
            dialog.DefaultButton = ContentDialogButton.Primary;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Walidacja
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    await ShowErrorDialogAsync("Krótka nazwa jest wymagana.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(fullNameTextBox.Text))
                {
                    await ShowErrorDialogAsync("Pełna nazwa jest wymagana.");
                    return;
                }

                if (!datePicker.Date.HasValue)
                {
                    await ShowErrorDialogAsync("Data jest wymagana.");
                    return;
                }

                var date = DateOnly.FromDateTime(datePicker.Date.Value.DateTime);

                // Dodaj do ViewModelu (start i end to ta sama data)
                ViewModel.AddCustomDayToList(nameTextBox.Text.Trim(), fullNameTextBox.Text.Trim(), date, date);
            }
        }

        private async Task ShowErrorDialogAsync(string message)
        {
            var dialog = App.CreateThemedDialog();
            dialog.Title = "Błąd";
            dialog.Content = message;
            dialog.CloseButtonText = "OK";
            await dialog.ShowAsync();
        }

        private async void WinterDatePicker_Opened(object sender, object e)
        {
            if (sender is CalendarDatePicker picker)
            {
                // Opóźnienie aby CalendarView został załadowany
                await System.Threading.Tasks.Task.Delay(50);

                // Znajdź CalendarView wewnątrz CalendarDatePicker za pomocą visual tree
                var calendarView = FindCalendarView(picker);
                if (calendarView != null)
                {
                    // Ustaw widok kalendarza na styczeń wybranego roku
                    var januaryDate = new DateTimeOffset(new DateTime(ViewModel.SelectedWinterYear, 1, 15));

                    // Zawsze ustaw widok na styczeń przy otwarciu (nawet jeśli data jest wybrana)
                    calendarView.SetDisplayDate(januaryDate);

                    // Wymuszenie polskiej lokalizacji
                    calendarView.Language = "pl-PL";
                    calendarView.FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday;
                }
            }
        }

        private void WinterDatePicker_CalendarViewDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
        {
            // Blokuj wszystkie dni oprócz sobót
            if (args.Phase == 0)
            {
                if (args.Item.Date.DayOfWeek != DayOfWeek.Saturday)
                {
                    args.Item.IsBlackout = true;
                }
            }
        }

        private CalendarView? FindCalendarView(DependencyObject parent)
        {
            int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is CalendarView calendarView)
                {
                    return calendarView;
                }

                var result = FindCalendarView(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
