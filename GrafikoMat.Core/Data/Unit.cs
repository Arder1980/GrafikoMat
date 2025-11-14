using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    [Table("units")]
    public class Unit : BaseModel, IComparable<Unit>
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("hospital_full_name")]
        public string HospitalFullName { get; set; } = string.Empty;

        [Column("department_name")]
        public string DepartmentName { get; set; } = string.Empty;

        [Column("is_archived")]
        public bool IsArchived { get; set; } = false;

        [Column("use_twelve_hour_shifts")]
        public bool UseTwelveHourShiftsByDefault { get; set; } = false;

        // ================== NOWA WŁAŚCIWOŚĆ ==================
        /// <summary>
        /// Gdy true, nieobsadzone sloty w grafiku będą oznaczane jako "Teleradiologia".
        /// </summary>
        [Column("allow_teleradiology_fallback")]
        public bool AllowTeleradiologyFallback { get; set; } = false;
        // ======================================================

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        public int CompareTo(Unit? other)
        {
            if (other == null) return 1;
            return string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}