using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using System;
using Windows.UI;
using WinRT;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Zarządza efektem akrylowego tła (backdrop) dla okna aplikacji.
    /// Centralizuje konfigurację kolorów i przezroczystości akrylu.
    /// </summary>
    public sealed class AcrylicBackdropManager : IDisposable
    {
        // ============================================
        // 🎨 PARAMETRY AKRYLU - TU SIĘ BAWISZ
        // ============================================

        /// <summary>
        /// Przezroczystość odcienia (0.0-1.0).
        /// Im NIŻSZA wartość, tym BARDZIEJ przezroczyste.
        /// Spróbuj: 0.3f, 0.5f, 0.7f
        /// </summary>
        private const float TINT_OPACITY = 0.5f;

        /// <summary>
        /// Przezroczystość luminancji (0.0-1.0).
        /// Im WYŻSZA wartość, tym JAŚNIEJSZY efekt.
        /// Spróbuj: 0.1f, 0.2f, 0.3f
        /// </summary>
        private const float LUMINOSITY_OPACITY = 0.2f;

        // Kolory dla motywu ciemnego
        private static readonly Color DARK_TINT_COLOR = Color.FromArgb(0xFF, 0x10, 0x10, 0x10);
        private static readonly Color DARK_FALLBACK_COLOR = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);

        // Kolory dla motywu jasnego - TU SIĘ BAWISZ
        // Spróbuj: 0xF0, 0xF5, 0xF8, 0xFA, 0xFC, 0xFE
        private static readonly Color LIGHT_TINT_COLOR = Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA);
        private static readonly Color LIGHT_FALLBACK_COLOR = Color.FromArgb(0xFF, 0xFE, 0xFE, 0xFE);

        // ============================================

        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _backdropConfiguration;
        private Window? _window;
        private bool _isDisposed;

        /// <summary>
        /// Inicjalizuje menedżera backdrop dla danego okna.
        /// </summary>
        /// <param name="window">Okno, dla którego ma być zastosowany efekt akrylowy</param>
        /// <returns>True jeśli inicjalizacja powiodła się, false w przeciwnym razie</returns>
        public bool Initialize(Window window)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AcrylicBackdropManager));

            if (!DesktopAcrylicController.IsSupported())
            {
                System.Diagnostics.Debug.WriteLine("AcrylicBackdropManager: DesktopAcrylicController not supported on this system");
                return false;
            }

            try
            {
                _window = window;
                _acrylicController = new DesktopAcrylicController();
                _backdropConfiguration = new SystemBackdropConfiguration();

                // Ustaw parametry przezroczystości
                _acrylicController.TintOpacity = TINT_OPACITY;
                _acrylicController.LuminosityOpacity = LUMINOSITY_OPACITY;

                // Obsługa zmiany motywu
                if (window.Content is FrameworkElement rootElement)
                {
                    rootElement.ActualThemeChanged += OnThemeChanged;

                    // Ustaw początkowy motyw
                    _backdropConfiguration.Theme = rootElement.ActualTheme switch
                    {
                        ElementTheme.Dark => SystemBackdropTheme.Dark,
                        ElementTheme.Light => SystemBackdropTheme.Light,
                        _ => SystemBackdropTheme.Default
                    };

                    UpdateColors(rootElement.ActualTheme);
                }

                // Podłącz backdrop do okna
                _acrylicController.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);

                System.Diagnostics.Debug.WriteLine("AcrylicBackdropManager: Successfully initialized");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AcrylicBackdropManager: Error during initialization: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// Ustawia czy okno jest aktywne (dla efektów backdrop).
        /// </summary>
        public void SetIsInputActive(bool isActive)
        {
            if (_backdropConfiguration != null)
            {
                _backdropConfiguration.IsInputActive = isActive;
            }
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            if (_backdropConfiguration != null)
            {
                _backdropConfiguration.Theme = sender.ActualTheme switch
                {
                    ElementTheme.Dark => SystemBackdropTheme.Dark,
                    ElementTheme.Light => SystemBackdropTheme.Light,
                    _ => SystemBackdropTheme.Default
                };

                UpdateColors(sender.ActualTheme);
            }
        }

        private void UpdateColors(ElementTheme theme)
        {
            if (_acrylicController == null) return;

            if (theme == ElementTheme.Dark)
            {
                _acrylicController.TintColor = DARK_TINT_COLOR;
                _acrylicController.FallbackColor = DARK_FALLBACK_COLOR;
            }
            else
            {
                _acrylicController.TintColor = LIGHT_TINT_COLOR;
                _acrylicController.FallbackColor = LIGHT_FALLBACK_COLOR;
            }

            System.Diagnostics.Debug.WriteLine($"AcrylicBackdropManager: Colors updated for {theme} theme");
        }

        private void Cleanup()
        {
            if (_window?.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= OnThemeChanged;
            }

            if (_acrylicController != null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
            }

            _backdropConfiguration = null;
            _window = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            Cleanup();
            _isDisposed = true;

            System.Diagnostics.Debug.WriteLine("AcrylicBackdropManager: Disposed");
        }
    }
}