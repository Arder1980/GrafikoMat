using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text.Json;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace GrafikoMat
{
    public partial class App : Application
    {
        public static MainWindow MainRoot { get; private set; }

        public App()
        {
            this.InitializeComponent();
            // Wyświetl ścieżkę do LocalFolder w oknie Output Visual Studio
            System.Diagnostics.Debug.WriteLine($"---> Ścieżka LocalFolder: {Windows.Storage.ApplicationData.Current.LocalFolder.Path}");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var orchestrator = new UxActionOrchestrator(WeakReferenceMessenger.Default);
            ServiceProvider.Register<IUxActionOrchestrator>(orchestrator);

            MainRoot = new MainWindow();

            // Ustaw motyw i stan okna PRZED aktywacją
            ApplyThemeEarly(MainRoot);
            ApplyInitialWindowState(MainRoot); // <-- DODAJ TĘ LINIĘ

            if (MainRoot.Content is FrameworkElement rootElement)
            {
                var initialTheme = rootElement.ActualTheme;
                ThemeManagerService.Instance.Initialize(initialTheme);
                ServiceProvider.Register(ThemeManagerService.Instance);
            }

            MainRoot.Activate();
        }        /// <summary>
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
        private void ApplyInitialWindowState(Window window)
        {
            // Ta metoda wczytuje ustawienia w ten sam sposób co ApplyThemeEarly
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

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                // Sprawdzamy, czy to pierwsze uruchomienie (brak zapisanej pozycji i nie był zmaksymalizowany)
                bool isFirstRun = !settings.WasWindowMaximized && settings.LastWindowPosition.X == 0 && settings.LastWindowPosition.Y == 0;

                if (isFirstRun)
                {
                    // --- LOGIKA DLA PIERWSZEGO URUCHOMIENIA ---
                    const int defaultWidth = 1600;
                    const int defaultHeight = 1000;

                    // Ustawiamy domyślny rozmiar
                    appWindow.Resize(new SizeInt32(defaultWidth, defaultHeight));

                    // Pobieramy informacje o ekranie, na którym jest okno
                    DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                    if (displayArea != null)
                    {
                        // Obliczamy pozycję, aby wyśrodkować okno
                        int centerX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                        int centerY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

                        // Przesuwamy okno na środek
                        appWindow.Move(new PointInt32(centerX, centerY));
                    }
                }
                else
                {
                    // --- LOGIKA DLA KOLEJNYCH URUCHOMIEŃ (BEZ ZMIAN) ---
                    if (settings.WasWindowMaximized && appWindow.Presenter is OverlappedPresenter op)
                    {
                        op.Maximize();
                    }
                    else
                    {
                        var lastSize = settings.LastWindowSize;
                        var lastPos = settings.LastWindowPosition;
                        appWindow.Resize(new SizeInt32(Math.Max(1600, lastSize.Width), Math.Max(1000, lastSize.Height)));
                        appWindow.Move(new PointInt32(lastPos.X, lastPos.Y));
                    }
                }
            }
        }
    }
}