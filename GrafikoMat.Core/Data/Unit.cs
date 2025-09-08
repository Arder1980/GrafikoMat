using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    [Table("units")]
    public class Unit : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("hospital_full_name")]
        public string HospitalFullName { get; set; } = string.Empty;

        [Column("department_name")]
        public string DepartmentName { get; set; } = string.Empty;

        // NOWA WŁAŚCIWOŚĆ: Status archiwizacji
        [Column("is_archived")]
        public bool IsArchived { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}