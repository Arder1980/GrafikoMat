namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// Reprezentuje wiersz w tabeli porównawczej silników.
    /// </summary>
    public record EngineComparisonInfo(
        string Name,
        string AlgorithmType,
        string Determinism,
        string Multithreading,
        string QualityGuarantee,
        string TimeToResult,
        string MemoryUsage
    );
}