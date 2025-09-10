using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Repositories
{
    /// <summary>
    /// Definiuje kontrakt dla operacji na przypisaniach lekarzy do jednostek.
    /// </summary>
    public interface IAssignmentRepository
    {
        /// <summary>
        /// Pobiera wszystkie istniejące przypisania.
        /// </summary>
        Task<List<UnitDoctorAssignment>> GetAllAsync();

        /// <summary>
        /// Pobiera wszystkie przypisania dla konkretnego lekarza.
        /// </summary>
        Task<List<UnitDoctorAssignment>> GetForDoctorAsync(Guid doctorId);
    }
}