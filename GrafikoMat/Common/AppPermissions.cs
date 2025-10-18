using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Common // lub GrafikoMat.Services
{
    /// <summary>
    /// Statyczna klasa centralizująca logikę sprawdzania uprawnień w aplikacji.
    /// Definiuje poziomy ról i udostępnia metody do weryfikacji dostępu do funkcji.
    /// </summary>
    public static class AppPermissions
    {
        // Poziomy uprawnień dla czytelności
        public const int UserLevel = 0;
        public const int AdminLevel = 1;
        public const int SuperAdminLevel = 9; // Lub wyższy, np. 10

        /// <summary> Sprawdza, czy użytkownik ma uprawnienia co najmniej Administratora. </summary>
        public static bool IsAdminOrHigher(int userLevel) => userLevel >= AdminLevel;

        /// <summary> Sprawdza, czy użytkownik ma uprawnienia Superadministratora. </summary>
        public static bool IsSuperAdmin(int userLevel) => userLevel >= SuperAdminLevel;

        /// <summary> Sprawdza, czy użytkownik może zarządzać użytkownikami (dodawać, edytować, archiwizować). </summary>
        public static bool CanManageUsers(int userLevel) => IsSuperAdmin(userLevel);

        /// <summary> Sprawdza, czy użytkownik może edytować deklaracje innych użytkowników. </summary>
        public static bool CanEditOtherDeclarations(int userLevel) => IsAdminOrHigher(userLevel);

        /// <summary> Sprawdza, czy użytkownik może zarządzać jednostkami (dodawać, edytować). </summary>
        public static bool CanManageUnits(int userLevel) => IsSuperAdmin(userLevel); // Tylko SuperAdmin

        /// <summary> Sprawdza, czy użytkownik ma dostęp do wszystkich opcji w ustawieniach. </summary>
        public static bool CanAccessAllSettings(int userLevel) => IsSuperAdmin(userLevel);

        /// <summary> Sprawdza, czy użytkownik ma dostęp do podstawowych opcji w ustawieniach (Wygląd, Priorytety, Silnik). </summary>
        public static bool CanAccessBasicSettings(int userLevel) => userLevel >= UserLevel; // Wszyscy

        /// <summary>
        /// Zwraca listę tytułów menu ustawień dozwolonych dla danego poziomu uprawnień,
        /// zachowując predefiniowaną kolejność.
        /// </summary>
        public static List<string> GetAllowedSettingsMenuItems(int userLevel)
        {
            var allowedItems = new List<string>
            {
                "Wygląd i Motyw",
                "Priorytety Obliczeń Grafiku",
                "Silnik Obliczeniowy"
            };

            // Tylko SuperAdmin widzi zarządzanie połączeniem i jednostkami
            if (IsSuperAdmin(userLevel))
            {
                allowedItems.Add("Połączenie z Bazą Danych");
                allowedItems.Add("Zarządzanie Jednostkami");
            }
            // Można tu dodać 'else if (IsAdminOrHigher(userLevel))' gdyby Admin miał coś więcej niż User

            // Predefiniowana kolejność wszystkich możliwych opcji
            var orderedTitles = new[] {
                "Wygląd i Motyw",                   // Index 0
                "Połączenie z Bazą Danych",        // Index 1
                "Zarządzanie Jednostkami",          // Index 2
                "Priorytety Obliczeń Grafiku",     // Index 3
                "Silnik Obliczeniowy"              // Index 4
            };

            // Sortuj dozwolone elementy zgodnie z predefiniowaną kolejnością
            return allowedItems.OrderBy(item => Array.IndexOf(orderedTitles, item)).ToList();
        }
    }
}