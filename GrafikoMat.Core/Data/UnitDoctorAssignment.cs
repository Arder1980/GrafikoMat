using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje powiązanie między lekarzem a jednostką.
    /// Mapuje się na tabelę 'unit_doctors' w bazie danych.
    /// </summary>
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