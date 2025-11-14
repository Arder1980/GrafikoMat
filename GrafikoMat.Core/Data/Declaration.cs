using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using GrafikoMat.Core.Enums;

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
        public DateTimeOffset LastModified { get; set; }

        // Pola współdyżurnych (top-level dla RLS i indeksów)
        [Column("co_duty_partner_id")]
        public Guid? CoDutyPartnerId { get; set; }

        [Column("co_duty_status")]
        public string? CoDutyStatus { get; set; }

        [Column("co_duty_initiator_id")]
        public Guid? CoDutyInitiatorId { get; set; }
    }

    /// <summary>
    /// Struktura JSON przechowująca deklaracje dla wszystkich dni miesiąca.
    /// </summary>
    public class DeclarationDataJson
    {
        [JsonProperty("days")]
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
        [JsonProperty("dayNumber")]
        public int Day { get; set; }

        /// <summary>
        /// Tryb dnia: Full24 lub Split12
        /// </summary>
        [JsonProperty("mode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DayMode Mode { get; set; } = DayMode.Full24;

        /// <summary>
        /// Deklaracja dla całodobowego dyżuru (MOG, CHC, WAR, REZ, URL, DYZ, ---)
        /// </summary>
        [JsonProperty("full")]
        public string? Full { get; set; }

        /// <summary>
        /// Deklaracja dla dyżuru dziennego (7:00-19:00)
        /// </summary>
        [JsonProperty("day")]
        public string? DaySlot { get; set; }

        /// <summary>
        /// Deklaracja dla dyżuru nocnego (19:00-7:00)
        /// </summary>
        [JsonProperty("night")]
        public string? Night { get; set; }

        /// <summary>
        /// ID partnera współdyżurnego (jeśli dotyczy)
        /// </summary>
        [JsonProperty("coDutyPartnerId")]
        public Guid? CoDutyPartnerId { get; set; }

        /// <summary>
        /// Status współdyżuru: Pending, Accepted, Rejected lub null
        /// </summary>
        [JsonProperty("coDutyStatus")]
        [JsonConverter(typeof(StringEnumConverter))]
        public CoDutyStatus? CoDutyStatus { get; set; }

        /// <summary>
        /// ID lekarza który zainicjował współdyżur (jeśli dotyczy)
        /// </summary>
        [JsonProperty("coDutyInitiatorId")]
        public Guid? CoDutyInitiatorId { get; set; }

        /// <summary>
        /// Który slot ma współdyżurnego: Full, Day, Night
        /// </summary>
        [JsonProperty("coDutySlotPart")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SlotPart? CoDutySlotPart { get; set; }
    }
}
