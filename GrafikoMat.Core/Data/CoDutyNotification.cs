using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Model reprezentujący powiadomienie o prośbie o współdyżur.
    /// Mapowany na tabelę 'co_duty_notifications' w Supabase.
    /// </summary>
    [Table("co_duty_notifications")]
    public class CoDutyNotification : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        [Column("id")]
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        public long Id { get; set; }

        [Column("from_doctor_id")]
        public Guid FromDoctorId { get; set; }

        [Column("to_doctor_id")]
        public Guid ToDoctorId { get; set; }

        [Column("unit_id")]
        public Guid UnitId { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("month")]
        public int Month { get; set; }

        [Column("day")]
        public int Day { get; set; }

        [Column("slot_part")]
        public string SlotPart { get; set; } = "full";

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("responded_at")]
        public DateTime? RespondedAt { get; set; }
    }
}
