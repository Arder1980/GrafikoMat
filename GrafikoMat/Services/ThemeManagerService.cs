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
        /// </summary>
        public ElementTheme CurrentTheme { get; private set; }

        private ThemeManagerService()
        {
            // Domyślnie startujemy z motywem jasnym
            CurrentTheme = ElementTheme.Light;
        }

        /// <summary>
        /// Inicjalizuje serwis na podstawie motywu wykrytego przy starcie aplikacji.
        /// </summary>
        public void Initialize(ElementTheme initialTheme)
        {
            CurrentTheme = initialTheme;
        }

        /// <summary>
        /// Ustawia nowy motyw. Jest to jedyne miejsce, w którym stan motywu jest zmieniany.
        /// </summary>
        public void SetTheme(ElementTheme newTheme)
        {
            CurrentTheme = newTheme;
        }
    }
}