using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Services;
using Microsoft.Extensions.DependencyInjection;
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
        public static MainWindow MainRoot { get; private set; } = null!;

        /// <summary>
        /// Kontener Dependency Injection dla całej aplikacji.
        /// </summary>
        public IServiceProvider Services { get; }

        // Win32 API do wykrywania DPI
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        public App()
        {
            Services = ConfigureServices();
            this.InitializeComponent();
            // Wyświetl ścieżkę do LocalFolder w oknie Output Visual Studio
            System.Diagnostics.Debug.WriteLine($"---> Ścieżka LocalFolder: {Windows.Storage.ApplicationData.Current.LocalFolder.Path}");
        }

        /// <summary>
        /// Konfiguruje Dependency Injection i rejestruje wszystkie serwisy.
        /// </summary>
        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Rejestracja podstawowych serwisów jako Singleton
            services.AddSingleton(WeakReferenceMessenger.Default);
            services.AddSingleton<IUxActionOrchestrator>(sp => new UxActionOrchestrator(WeakReferenceMessenger.Default));
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton(ThemeManagerService.Instance);

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainRoot = new MainWindow();

            // Ustaw motyw i stan okna PRZED aktywacją
            ApplyThemeEarly(MainRoot);
            ApplyInitialWindowState(MainRoot);

            if (MainRoot.Content is FrameworkElement rootElement)
            {
                var initialTheme = rootElement.ActualTheme;
                ThemeManagerService.Instance.Initialize(initialTheme);
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
            catch (Exception ex)
            {
                // POPRAWKA ŚREDNIA: Loguj szczegóły błędu zamiast po cichu ignorować
                System.Diagnostics.Debug.WriteLine($"[App.ApplyThemeEarly] Error loading settings: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[App.ApplyThemeEarly] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App.ApplyThemeEarly] Stack trace: {ex.StackTrace}");
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
            catch (Exception ex)
            {
                // POPRAWKA ŚREDNIA: Loguj szczegóły błędu zamiast po cichu ignorować
                System.Diagnostics.Debug.WriteLine($"[App.ApplyInitialWindowState] Error loading settings: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[App.ApplyInitialWindowState] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App.ApplyInitialWindowState] Stack trace: {ex.StackTrace}");
                settings = new AppSettings();
            }

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Oblicz DPI scale factor dla ekranu używając Win32 API
                double scaleFactor = 1.0;
                try
                {
                    uint dpi = GetDpiForWindow(hwnd);
                    scaleFactor = dpi / 96.0; // 96 DPI = 100% scaling
                    System.Diagnostics.Debug.WriteLine($"ApplyInitialWindowState: Detected DPI={dpi}, scale={scaleFactor:F2}x");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ApplyInitialWindowState: Failed to get DPI: {ex.Message}");
                    scaleFactor = 1.0;
                }

                // Bazowe rozmiary (logiczne)
                const int baseWidth = 1600;
                const int baseHeight = 1000;

                // Przeskalowane rozmiary (fizyczne)
                int scaledWidth = (int)Math.Ceiling(baseWidth * scaleFactor);
                int scaledHeight = (int)Math.Ceiling(baseHeight * scaleFactor);

                System.Diagnostics.Debug.WriteLine($"ApplyInitialWindowState: Base={baseWidth}×{baseHeight}, Scaled={scaledWidth}×{scaledHeight}");

                // Sprawdzamy, czy to pierwsze uruchomienie (brak zapisanej pozycji i nie był zmaksymalizowany)
                bool isFirstRun = !settings.WasWindowMaximized &&
                                  settings.LastWindowPosition.X == 0 &&
                                  settings.LastWindowPosition.Y == 0;

                if (isFirstRun)
                {
                    // --- LOGIKA DLA PIERWSZEGO URUCHOMIENIA ---
                    // WAŻNE: Najpierw jawnie przywróć okno (jeśli było zminimalizowane)
                    presenter.Restore();

                    // Ustawiamy domyślny rozmiar (DPI-aware)
                    appWindow.Resize(new SizeInt32(scaledWidth, scaledHeight));

                    // Pobieramy informacje o ekranie, na którym jest okno
                    DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                    if (displayArea != null)
                    {
                        // Obliczamy pozycję, aby wyśrodkować okno
                        int centerX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - scaledWidth) / 2;
                        int centerY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - scaledHeight) / 2;

                        // Przesuwamy okno na środek
                        appWindow.Move(new PointInt32(centerX, centerY));
                    }
                }
                else
                {
                    // --- LOGIKA DLA KOLEJNYCH URUCHOMIEŃ ---
                    if (settings.WasWindowMaximized)
                    {
                        // Jeśli było zmaksymalizowane, maksymalizuj
                        presenter.Maximize();
                    }
                    else
                    {
                        // WAŻNE: Najpierw jawnie przywróć okno
                        presenter.Restore();

                        // Potem ustaw rozmiar i pozycję (z DPI-aware minimum)
                        var lastSize = settings.LastWindowSize;
                        var lastPos = settings.LastWindowPosition;
                        appWindow.Resize(new SizeInt32(Math.Max(scaledWidth, lastSize.Width), Math.Max(scaledHeight, lastSize.Height)));
                        appWindow.Move(new PointInt32(lastPos.X, lastPos.Y));
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Window state applied: isFirstRun={isFirstRun}, wasMaximized={settings.WasWindowMaximized}, state={presenter.State}");
            }
        }
    }
}