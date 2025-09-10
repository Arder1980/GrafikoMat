using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Jedno źródło prawdy dla deklaracji dostępności.
    /// Definiuje semantykę, kody i nazwy wyświetlane dla każdego typu dostępności.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public static class Declarations
    {
        private static readonly IReadOnlyDictionary<AvailabilityType, string> _codes =
            new Dictionary<AvailabilityType, string>
            {
                { AvailabilityType.Unavailable, "---" },
                { AvailabilityType.ConditionallyAvailable, "WAR" },
                { AvailabilityType.Available, "MOG" },
                { AvailabilityType.Wants, "CHC" },
                { AvailabilityType.Reservation, "REZ" },
                { AvailabilityType.OtherDuty, "DYZ" },
                { AvailabilityType.Vacation, "URL" },
            };

        public static string GetCode(AvailabilityType t) => _codes[t];

        public static string GetFullName(AvailabilityType t) => t switch
        {
            AvailabilityType.Unavailable => "---",
            AvailabilityType.ConditionallyAvailable => "Mogę warunkowo",
            AvailabilityType.Available => "Mogę",
            AvailabilityType.Wants => "Chcę",
            AvailabilityType.Reservation => "Rezerwacja",
            AvailabilityType.OtherDuty => "Dyżur (inny)",
            AvailabilityType.Vacation => "Urlop",
            _ => t.ToString()
        };

        public static bool AllowsAssignment(AvailabilityType t) =>
            t is AvailabilityType.Available
             or AvailabilityType.ConditionallyAvailable
             or AvailabilityType.Wants
             or AvailabilityType.Reservation;

        public static bool IsHardBlock(AvailabilityType t) =>
            t is AvailabilityType.Unavailable
             or AvailabilityType.Vacation
             or AvailabilityType.OtherDuty;

        public static bool IsReservation(AvailabilityType t) =>
            t == AvailabilityType.Reservation;

        public static bool BypassesAdjacencyRules(AvailabilityType t) => false;

        public static bool IsConditional(AvailabilityType t) =>
            t == AvailabilityType.ConditionallyAvailable;

        public static int GetPreferenceWeight(AvailabilityType t) => t switch
        {
            AvailabilityType.Wants => 2,
            AvailabilityType.Available => 1,
            AvailabilityType.ConditionallyAvailable => 0,
            _ => -100
        };
    }
}