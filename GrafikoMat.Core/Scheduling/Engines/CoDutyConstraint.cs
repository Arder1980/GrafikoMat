using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Klasa walidująca i wymuszająca constraints dla współdyżurnych.
    /// </summary>
    public static class CoDutyConstraint
    {
        /// <summary>
        /// Waliduje czy wszystkie zaakceptowane pary współdyżurnych są razem lub wcale w grafiku.
        /// </summary>
        /// <param name="declarations">Lista wszystkich deklaracji</param>
        /// <param name="schedule">Grafik [doctorIndex, dayIndex] gdzie wartość > 0 oznacza przypisanie</param>
        /// <param name="doctorIndexMap">Mapowanie Guid lekarza na jego indeks w grafiku</param>
        /// <returns>True jeśli wszystkie pary są spełnione, false w przeciwnym razie</returns>
        public static bool ValidateCoDutyPairs(
            List<Declaration> declarations,
            int[,] schedule,
            Dictionary<Guid, int> doctorIndexMap)
        {
            if (declarations == null || schedule == null || doctorIndexMap == null)
                return true;

            // Znajdź wszystkie zaakceptowane pary współdyżurnych
            var acceptedPairs = declarations
                .Where(d => d.DeclarationDataJson?.Days != null)
                .SelectMany(d => d.DeclarationDataJson.Days
                    .Where(day => day.CoDutyStatus == Enums.CoDutyStatus.Accepted && day.CoDutyPartnerId != null)
                    .Select(day => new
                    {
                        DoctorId = d.DoctorId,
                        PartnerId = day.CoDutyPartnerId.Value,
                        Year = d.Year,
                        Month = d.Month,
                        Day = day.Day,
                        Mode = day.Mode
                    }))
                .ToList();

            foreach (var pair in acceptedPairs)
            {
                var dayIndex = pair.Day - 1;

                if (!doctorIndexMap.TryGetValue(pair.DoctorId, out int doctorIdx) ||
                    !doctorIndexMap.TryGetValue(pair.PartnerId, out int partnerIdx))
                    continue;

                var doctorAssigned = schedule[doctorIdx, dayIndex] > 0;
                var partnerAssigned = schedule[partnerIdx, dayIndex] > 0;

                // HARD CONSTRAINT: MUSZĄ być razem lub wcale
                if (doctorAssigned != partnerAssigned)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Automatycznie przypisuje partnera gdy jeden z pary jest przypisywany.
        /// </summary>
        /// <param name="declarations">Lista wszystkich deklaracji</param>
        /// <param name="schedule">Grafik do modyfikacji</param>
        /// <param name="doctorIndexMap">Mapowanie Guid lekarza na jego indeks</param>
        /// <param name="dayIndex">Indeks dnia (0-based)</param>
        /// <param name="doctorIndex">Indeks lekarza który jest przypisywany</param>
        /// <param name="assignmentValue">Wartość przypisania (1=dzienny, 2=nocny, itd.)</param>
        public static void ApplyCoDutyPairs(
            List<Declaration> declarations,
            int[,] schedule,
            Dictionary<Guid, int> doctorIndexMap,
            int dayIndex,
            int doctorIndex,
            int assignmentValue)
        {
            if (declarations == null || schedule == null || doctorIndexMap == null)
                return;

            // Znajdź lekarza po indeksie
            var doctorEntry = doctorIndexMap.FirstOrDefault(kvp => kvp.Value == doctorIndex);
            if (doctorEntry.Key == Guid.Empty)
                return;

            var doctorId = doctorEntry.Key;

            // Znajdź deklarację tego lekarza
            var declaration = declarations.FirstOrDefault(d => d.DoctorId == doctorId);
            if (declaration?.DeclarationDataJson?.Days == null)
                return;

            var dayDecl = declaration.DeclarationDataJson.Days.FirstOrDefault(d => d.Day == dayIndex + 1);
            if (dayDecl == null || dayDecl.CoDutyStatus != Enums.CoDutyStatus.Accepted || dayDecl.CoDutyPartnerId == null)
                return;

            // Przypisz partnera tak samo
            if (doctorIndexMap.TryGetValue(dayDecl.CoDutyPartnerId.Value, out int partnerIdx))
            {
                schedule[partnerIdx, dayIndex] = assignmentValue;
            }
        }

        /// <summary>
        /// Cofa przypisanie partnera gdy jeden z pary jest cofany.
        /// </summary>
        public static void UnapplyCoDutyPairs(
            List<Declaration> declarations,
            int[,] schedule,
            Dictionary<Guid, int> doctorIndexMap,
            int dayIndex,
            int doctorIndex)
        {
            if (declarations == null || schedule == null || doctorIndexMap == null)
                return;

            var doctorEntry = doctorIndexMap.FirstOrDefault(kvp => kvp.Value == doctorIndex);
            if (doctorEntry.Key == Guid.Empty)
                return;

            var doctorId = doctorEntry.Key;
            var declaration = declarations.FirstOrDefault(d => d.DoctorId == doctorId);
            if (declaration?.DeclarationDataJson?.Days == null)
                return;

            var dayDecl = declaration.DeclarationDataJson.Days.FirstOrDefault(d => d.Day == dayIndex + 1);
            if (dayDecl == null || dayDecl.CoDutyStatus != Enums.CoDutyStatus.Accepted || dayDecl.CoDutyPartnerId == null)
                return;

            // Wyczyść partnera
            if (doctorIndexMap.TryGetValue(dayDecl.CoDutyPartnerId.Value, out int partnerIdx))
            {
                schedule[partnerIdx, dayIndex] = 0;
            }
        }
    }
}
