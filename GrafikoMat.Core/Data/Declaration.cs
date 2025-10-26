using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Model reprezentujący deklaracje dyżurowe lekarza na dany miesiąc w konkretnej jednostce.
    /// Mapowany na tabelę 'declarations' w Supabase.
    /// </summary>
    [Table("declarations")]
    public class Declaration : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        [Column("id")]
        [Newtonsoft.Json.JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long Id { get; set; }

        [Column("unit_id")]
        public Guid UnitId { get; set; }

        [Column("doctor_id")]
        public Guid DoctorId { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("month")]
        public int Month { get; set; }

        [Column("declaration_data_json")]
        public DeclarationDataJson? DeclarationDataJson { get; set; }

        [Column("last_modified")]
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Struktura JSON przechowująca deklaracje dla wszystkich dni miesiąca.
    /// </summary>
    public class DeclarationDataJson
    {
        [JsonPropertyName("days")]
        public List<DayDeclarationDto> Days { get; set; } = new();
    }

    /// <summary>
    /// Deklaracja dla pojedynczego dnia.
    /// </summary>
    public class DayDeclarationDto
    {
        /// <summary>
        /// Numer dnia w miesiącu (1-31)
        /// </summary>
        [JsonPropertyName("dayNumber")]
        public int Day { get; set; }

        /// <summary>
        /// Tryb dnia: "Full24" lub "Split12"
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "Full24";

        /// <summary>
        /// Deklaracja dla całodobowego dyżuru (MOG, CHC, WAR, REZ, URL, DYZ, ---)
        /// </summary>
        [JsonPropertyName("full")]
        public string? Full { get; set; }

        /// <summary>
        /// Deklaracja dla dyżuru dziennego (7:00-19:00)
        /// </summary>
        [JsonPropertyName("day")]
        public string? DaySlot { get; set; }

        /// <summary>
        /// Deklaracja dla dyżuru nocnego (19:00-7:00)
        /// </summary>
        [JsonPropertyName("night")]
        public string? Night { get; set; }

        /// <summary>
        /// ID partnera współdyżurnego (jeśli dotyczy)
        /// </summary>
        [JsonPropertyName("coDutyPartnerId")]
        public Guid? CoDutyPartnerId { get; set; }

        /// <summary>
        /// Status współdyżuru: "pending", "accepted", "rejected" lub null
        /// </summary>
        [JsonPropertyName("coDutyStatus")]
        public string? CoDutyStatus { get; set; }

        /// <summary>
        /// ID lekarza który zainicjował współdyżur (jeśli dotyczy)
        /// </summary>
        [JsonPropertyName("coDutyInitiatorId")]
        public Guid? CoDutyInitiatorId { get; set; }

        /// <summary>
        /// Który slot ma współdyżurnego: "full", "day", "night"
        /// </summary>
        [JsonPropertyName("coDutySlotPart")]
        public string? CoDutySlotPart { get; set; }
    }
}
