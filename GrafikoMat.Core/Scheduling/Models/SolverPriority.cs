using System.ComponentModel;

namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Definiuje możliwe kryteria optymalizacji (priorytety), według których silnik ocenia jakość grafiku.
    /// </summary>
    public enum SolverPriority
    {
        [Description("Ciągłość obsady")]
        InitialContinuity = 0,

        [Description("Obsada (łączna)")]
        TotalAssignments = 1,

        [Description("Sprawiedliwość (σ obciążeń)")]
        Fairness = 2,

        [Description("Równomierność (czasowa)")]
        Spacing = 3,

        // PRZYWRÓCONY PRIORYTET
        [Description("Zgodność z ważnością deklaracji")]
        DeclarationCompliance = 4
    }
}