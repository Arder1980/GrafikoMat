using System;

namespace GrafikoMat.Common
{
    /// <summary>
    /// Pomocnik do generowania haseł startowych.
    /// </summary>
    public static class PasswordGenerator
    {
        // Używamy jednej instancji Random dla lepszej losowości,
        // aby uniknąć problemu tworzenia wielu instancji w krótkim czasie,
        // co mogłoby skutkować takimi samymi "losowymi" liczbami.
        private static readonly Random _random = new Random();

        /// <summary>
        /// Generuje nowe, losowe hasło startowe w formacie 'GrafikoMat!xxxx'.
        /// </summary>
        /// <returns>Nowe hasło startowe jako string.</returns>
        public static string GenerateInitialPassword()
        {
            int randomNumber = _random.Next(1000, 10000); // Generuje liczbę od 1000 do 9999
            return $"GrafikoMat!{randomNumber}";
        }
    }
}