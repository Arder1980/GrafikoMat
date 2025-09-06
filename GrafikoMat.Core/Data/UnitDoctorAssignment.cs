using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    [Table("unit_doctors")]
    public class UnitDoctorAssignment : BaseModel
    {
        [PrimaryKey("unit_id", true)]
        public Guid UnitId { get; set; }

        [PrimaryKey("doctor_id", true)]
        public Guid DoctorId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
