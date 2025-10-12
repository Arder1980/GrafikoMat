using System;
using System.Text;

namespace GrafikoMat.Common
{
    /// <summary>
    /// Pomocnik do generowania haseł startowych.
    /// </summary>
    public static class PasswordGenerator
    {
        private static readonly Random _random = new Random();

        // ZMIANA: Zdefiniowano zestawy znaków do losowania
        private const string SpecialChars = "!@#$%^&*()_+-=";
        private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>
        /// Generuje nowe, losowe hasło startowe w formacie 'GrafikoMat' + ZnakSpecjalny + 6 znaków alfanumerycznych.
        /// </summary>
        /// <returns>Nowe, silne hasło startowe jako string.</returns>
        public static string GenerateInitialPassword()
        {
            var passwordBuilder = new StringBuilder();
            passwordBuilder.Append("GrafikoMat");

            // Krok 1: Dodaj jeden losowy znak specjalny
            int specialCharIndex = _random.Next(SpecialChars.Length);
            passwordBuilder.Append(SpecialChars[specialCharIndex]);

            // Krok 2: Dodaj sześć losowych znaków alfanumerycznych
            for (int i = 0; i < 6; i++)
            {
                int alphanumericCharIndex = _random.Next(AlphanumericChars.Length);
                passwordBuilder.Append(AlphanumericChars[alphanumericCharIndex]);
            }

            return passwordBuilder.ToString();
        }
    }
}