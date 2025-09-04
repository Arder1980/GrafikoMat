using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class ProfilesSettingsView : UserControl
    {
        public event Action? ReloadRequired;

        private readonly ObservableCollection<UnitProfile> Profiles = new();

        private SettingsService? _settingsService;
        private AppSettings? _appSettings;

        public ProfilesSettingsView()
        {
            this.InitializeComponent();
        }

        public void Initialize(SettingsService service, AppSettings settings)
        {
            _settingsService = service;
            _appSettings = settings;

            Profiles.Clear();
            if (_appSettings?.UnitProfiles != null)
            {
                foreach (var profile in _appSettings.UnitProfiles.OrderBy(p => p.Name))
                {
                    Profiles.Add(profile);
                }
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowProfileDialogAsync(null);
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesListView.SelectedItem is UnitProfile selectedProfile)
            {
                await ShowProfileDialogAsync(selectedProfile);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesListView.SelectedItem is UnitProfile selectedProfile && _appSettings != null && _settingsService != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Potwierdź usunięcie",
                    Content = $"Czy na pewno chcesz usunąć profil '{selectedProfile.Name}'? Tej operacji nie można cofnąć.",
                    PrimaryButtonText = "Usuń",
                    CloseButtonText = "Anuluj",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var updatedProfiles = _appSettings.UnitProfiles.Where(p => p.Id != selectedProfile.Id).ToList();
                    var newActiveId = _appSettings.ActiveUnitProfileId == selectedProfile.Id ? null : _appSettings.ActiveUnitProfileId;
                    _appSettings = _appSettings with { UnitProfiles = updatedProfiles, ActiveUnitProfileId = newActiveId };

                    await _settingsService.SaveSettingsAsync(_appSettings);
                    Initialize(_settingsService, _appSettings);
                }
            }
        }

        private async void SetActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesListView.SelectedItem is UnitProfile selectedProfile && _appSettings != null && _settingsService != null)
            {
                var newSettings = _appSettings with { ActiveUnitProfileId = selectedProfile.Id };
                await _settingsService.SaveSettingsAsync(newSettings);

                var dialog = new ContentDialog
                {
                    Title = "Profil aktywowany",
                    Content = $"Profil '{selectedProfile.Name}' jest teraz aktywny. Aplikacja zostanie przeładowana, aby wczytać nowe dane.",
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                ReloadRequired?.Invoke();
            }
        }

        private async Task ShowProfileDialogAsync(UnitProfile? existingProfile)
        {
            if (_settingsService == null || _appSettings == null) return;

            bool isEditMode = existingProfile != null;

            var nameTextBox = new TextBox { Header = "Nazwa skrócona (np. Szpital Miejski)", Text = existingProfile?.Name ?? "" };
            var hospitalNameTextBox = new TextBox { Header = "Pełna nazwa szpitala", Text = existingProfile?.HospitalFullName ?? "" };
            var departmentNameTextBox = new TextBox { Header = "Nazwa oddziału/zakładu", Text = existingProfile?.DepartmentName ?? "" };
            var urlTextBox = new TextBox { Header = "Supabase URL", Text = existingProfile?.SupabaseUrl ?? "" };
            var keyTextBox = new TextBox { Header = "Supabase Anon Key", Text = existingProfile?.SupabaseApiKey ?? "" };

            var panel = new StackPanel { Spacing = 12, Children = { nameTextBox, hospitalNameTextBox, departmentNameTextBox, urlTextBox, keyTextBox } };

            var dialog = new ContentDialog
            {
                Title = isEditMode ? "Edytuj profil jednostki" : "Dodaj nowy profil",
                Content = panel,
                PrimaryButtonText = "Zapisz",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var newProfile = new UnitProfile
                {
                    Id = existingProfile?.Id ?? Guid.NewGuid(),
                    Name = nameTextBox.Text,
                    HospitalFullName = hospitalNameTextBox.Text,
                    DepartmentName = departmentNameTextBox.Text,
                    SupabaseUrl = urlTextBox.Text,
                    SupabaseApiKey = keyTextBox.Text
                };

                var updatedProfiles = _appSettings.UnitProfiles.Where(p => p.Id != newProfile.Id).ToList();
                updatedProfiles.Add(newProfile);
                _appSettings = _appSettings with { UnitProfiles = updatedProfiles };

                await _settingsService.SaveSettingsAsync(_appSettings);
                Initialize(_settingsService, _appSettings);
            }
        }
    }
}