using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje pojedynczą jednostkę (szpital/oddział).
    /// Mapuje się na tabelę 'units' w bazie danych.
    /// </summary>
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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}