using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Text.Json.Serialization;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Model reprezentujący dni specjalne (ferie zimowe, święta lokalne, itp.)
    /// Może być globalny (UnitId == null) lub per jednostka (UnitId != null)
    /// </summary>
    [Table("special_days")]
    public class SpecialDay : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Typ dnia specjalnego: 'winter_holiday', 'summer_holiday', 'custom'
        /// </summary>
        [Column("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Krótka nazwa (max 15 znaków) - wyświetlana w nagłówku komórki kalendarza
        /// </summary>
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Pełna nazwa - wyświetlana w tooltip
        /// </summary>
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Data rozpoczęcia
        /// </summary>
        [Column("start_date")]
        public DateOnly StartDate { get; set; }

        /// <summary>
        /// Data zakończenia
        /// </summary>
        [Column("end_date")]
        public DateOnly EndDate { get; set; }

        /// <summary>
        /// Rok (dla łatwiejszego filtrowania)
        /// </summary>
        [Column("year")]
        public int Year { get; set; }

        /// <summary>
        /// ID jednostki (NULL = globalne dla wszystkich jednostek)
        /// </summary>
        [Column("unit_id")]
        public Guid? UnitId { get; set; }

        /// <summary>
        /// Czy to przerwa szkolna (np. ferie)
        /// </summary>
        [Column("is_school_break")]
        public bool IsSchoolBreak { get; set; }

        /// <summary>
        /// Data utworzenia
        /// </summary>
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Data ostatniej aktualizacji
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Sprawdza czy dzień specjalny jest aktywny dla danej daty
        /// </summary>
        public bool IsActiveOn(DateOnly date)
        {
            return date >= StartDate && date <= EndDate;
        }

        /// <summary>
        /// Sprawdza czy jest globalny (nie przypisany do konkretnej jednostki)
        /// </summary>
        public bool IsGlobal => !UnitId.HasValue;
    }

    /// <summary>
    /// Typy dni specjalnych
    /// </summary>
    public static class SpecialDayTypes
    {
        public const string WinterHoliday = "winter_holiday";
        public const string SummerHoliday = "summer_holiday";
        public const string Custom = "custom";
    }
}
