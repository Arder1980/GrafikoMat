using Microsoft.UI.Xaml;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Centralny serwis ("Wyrocznia") przechowujący stan aktualnego motywu aplikacji.
    /// Działa jako Singleton, zapewniając jedno, spójne źródło prawdy.
    /// </summary>
    public sealed class ThemeManagerService
    {
        // Prosta implementacja wzorca Singleton
        public static ThemeManagerService Instance { get; } = new ThemeManagerService();

        /// <summary>
        /// Przechowuje aktualnie obowiązujący motyw w aplikacji.
        /// Volatile gwarantuje atomowość odczytu/zapisu i zapobiega race conditions.
        /// </summary>
        private volatile ElementTheme _currentTheme;

        public ElementTheme CurrentTheme => _currentTheme;

        private ThemeManagerService()
        {
            // Domyślnie startujemy z motywem jasnym
            _currentTheme = ElementTheme.Light;
        }

        /// <summary>
        /// Inicjalizuje serwis na podstawie motywu wykrytego przy starcie aplikacji.
        /// </summary>
        public void Initialize(ElementTheme initialTheme)
        {
            _currentTheme = initialTheme;
        }

        /// <summary>
        /// Ustawia nowy motyw. Jest to jedyne miejsce, w którym stan motywu jest zmieniany.
        /// Volatile zapewnia thread-safety.
        /// </summary>
        public void SetTheme(ElementTheme newTheme)
        {
            _currentTheme = newTheme;
        }
    }
}