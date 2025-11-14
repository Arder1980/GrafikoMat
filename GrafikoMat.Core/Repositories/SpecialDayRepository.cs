using GrafikoMat.Core.Data;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    public class SpecialDayRepository : ISpecialDayRepository
    {
        private readonly Client _supabase;

        public SpecialDayRepository(Client supabase)
        {
            _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        }

        public async Task<List<SpecialDay>> GetSpecialDaysForYearAsync(int year, Guid? unitId = null)
        {
            // Pobierz wszystkie dni specjalne dla danego roku
            var response = await _supabase
                .From<SpecialDay>()
                .Where(sd => sd.Year == year)
                .Get().ConfigureAwait(false);

            var allDays = response.Models ?? new List<SpecialDay>();

            // Jeśli unitId podane - pobierz globalne + dla tej jednostki
            // Jeśli unitId == null - pobierz tylko globalne
            if (unitId.HasValue)
            {
                return allDays.Where(sd => sd.UnitId == null || sd.UnitId == unitId.Value).ToList();
            }
            else
            {
                return allDays.Where(sd => sd.UnitId == null).ToList();
            }
        }

        public async Task<List<SpecialDay>> GetSpecialDaysByTypeAsync(int year, string type, Guid? unitId = null)
        {
            // Pobierz wszystkie dni specjalne dla danego roku i typu
            var response = await _supabase
                .From<SpecialDay>()
                .Where(sd => sd.Year == year && sd.Type == type)
                .Get().ConfigureAwait(false);

            var allDays = response.Models ?? new List<SpecialDay>();

            // Jeśli unitId podane - pobierz globalne + dla tej jednostki
            // Jeśli unitId == null - pobierz tylko globalne
            if (unitId.HasValue)
            {
                return allDays.Where(sd => sd.UnitId == null || sd.UnitId == unitId.Value).ToList();
            }
            else
            {
                return allDays.Where(sd => sd.UnitId == null).ToList();
            }
        }

        public async Task<SpecialDay?> GetSpecialDayByIdAsync(Guid id)
        {
            var response = await _supabase
                .From<SpecialDay>()
                .Where(sd => sd.Id == id)
                .Single();

            return response;
        }

        public async Task<SpecialDay?> GetSpecialDayForDateAsync(DateOnly date, Guid? unitId = null)
        {
            var year = date.Year;
            var allDays = await GetSpecialDaysForYearAsync(year, unitId).ConfigureAwait(false);

            // Priorytet: najpierw jednostkowe, potem globalne
            return allDays
                .Where(sd => sd.IsActiveOn(date))
                .OrderByDescending(sd => sd.UnitId.HasValue) // Jednostkowe najpierw
                .FirstOrDefault();
        }

        public async Task<SpecialDay> CreateSpecialDayAsync(SpecialDay specialDay)
        {
            // Walidacja
            if (specialDay.StartDate > specialDay.EndDate)
                throw new ArgumentException("Data rozpoczęcia nie może być późniejsza niż data zakończenia");

            if (specialDay.Name.Length > 15)
                throw new ArgumentException("Nazwa może mieć maksymalnie 15 znaków");

            if (specialDay.FullName.Length > 200)
                throw new ArgumentException("Pełna nazwa może mieć maksymalnie 200 znaków");

            // Ustaw rok na podstawie start_date
            specialDay.Year = specialDay.StartDate.Year;
            specialDay.CreatedAt = DateTime.UtcNow;
            specialDay.UpdatedAt = DateTime.UtcNow;

            var response = await _supabase
                .From<SpecialDay>()
                .Insert(specialDay);

            return response.Models.First();
        }

        public async Task<SpecialDay> UpdateSpecialDayAsync(SpecialDay specialDay)
        {
            // Walidacja
            if (specialDay.StartDate > specialDay.EndDate)
                throw new ArgumentException("Data rozpoczęcia nie może być późniejsza niż data zakończenia");

            if (specialDay.Name.Length > 15)
                throw new ArgumentException("Nazwa może mieć maksymalnie 15 znaków");

            if (specialDay.FullName.Length > 200)
                throw new ArgumentException("Pełna nazwa może mieć maksymalnie 200 znaków");

            specialDay.Year = specialDay.StartDate.Year;
            specialDay.UpdatedAt = DateTime.UtcNow;

            var response = await _supabase
                .From<SpecialDay>()
                .Update(specialDay);

            return response.Models.First();
        }

        public async Task DeleteSpecialDayAsync(Guid id)
        {
            await _supabase
                .From<SpecialDay>()
                .Where(sd => sd.Id == id)
                .Delete().ConfigureAwait(false);
        }

        public async Task<SpecialDay> GetOrCreateWinterHolidayAsync(int year, Guid? unitId = null)
        {
            // Sprawdź czy istnieje
            var existing = await GetSpecialDaysByTypeAsync(year, SpecialDayTypes.WinterHoliday, unitId).ConfigureAwait(false);

            // Filtruj dokładnie - jeśli szukamy globalnych (unitId == null), bierz tylko globalne
            // Jeśli szukamy dla jednostki, bierz dla tej jednostki
            existing = existing
                .Where(sd => unitId.HasValue ? sd.UnitId == unitId : sd.UnitId == null)
                .ToList();

            if (existing.Any())
                return existing.First();

            // Jeśli nie istnieje - utwórz pusty (admin będzie musiał ustawić daty)
            var winterHoliday = new SpecialDay
            {
                Type = SpecialDayTypes.WinterHoliday,
                Name = "Ferie zimowe",
                FullName = $"Ferie zimowe {year}",
                // Domyślne daty - typowo koniec stycznia
                StartDate = new DateOnly(year, 1, 20),
                EndDate = new DateOnly(year, 2, 2),
                Year = year,
                UnitId = unitId,
                IsSchoolBreak = true
            };

            return await CreateSpecialDayAsync(winterHoliday).ConfigureAwait(false);
        }
    }
}
