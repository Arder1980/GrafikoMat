using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace GrafikoMat
{
    public partial class App : Application
    {
        public static MainWindow MainRoot { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Rejestracja globalnego orkiestratora
            var orchestrator = new UxActionOrchestrator(WeakReferenceMessenger.Default);
            ServiceProvider.Register<IUxActionOrchestrator>(orchestrator);

            MainRoot = new MainWindow();

            // ZMIANA: Ustaw motyw PRZED aktywacją okna, aby uniknąć mignięcia
            ApplyThemeEarly(MainRoot);

            MainRoot.Activate();
        }

        /// <summary>
        /// Wczytuje ustawienia i natychmiastowo aplikuje motyw, aby uniknąć mignięcia przy starcie.
        /// </summary>
        private void ApplyThemeEarly(Window window)
        {
            AppSettings settings;
            try
            {
                var settingsPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    settings = new AppSettings();
                }
            }
            catch
            {
                settings = new AppSettings();
            }

            if (window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = settings.Theme switch
                {
                    AppTheme.Light => ElementTheme.Light,
                    AppTheme.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
        }

        /// <summary>
        /// Tworzy ContentDialog z poprawnie ustawionym XamlRoot i motywem.
        /// Należy używać tej metody we wszystkich miejscach w aplikacji.
        /// </summary>
        public static ContentDialog CreateThemedDialog()
        {
            var dialog = new ContentDialog();
            if (MainRoot?.Content is FrameworkElement rootElement)
            {
                dialog.XamlRoot = rootElement.XamlRoot;
                dialog.RequestedTheme = rootElement.ActualTheme;
            }
            return dialog;
        }
    }
}