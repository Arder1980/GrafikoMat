using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// Reprezentuje pojedynczy silnik na liście wyboru w UI.
    /// Łączy w sobie dane opisu i dane do tabeli porównawczej.
    /// </summary>
    public class EngineOption : ObservableObject
    {
        // Dane podstawowe
        public SolverType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public string Category { get; }

        // Dane do tabeli porównawczej
        public string AlgorithmType { get; }
        public string Determinism { get; }
        public string Multithreading { get; }
        public string QualityGuarantee { get; }
        public string TimeToResult { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public EngineOption(SolverType type, string name, string description, string category,
            string algorithmType, string determinism, string multithreading, string qualityGuarantee, string timeToResult)
        {
            Type = type;
            Name = name;
            Description = description;
            Category = category;
            AlgorithmType = algorithmType;
            Determinism = determinism;
            Multithreading = multithreading;
            QualityGuarantee = qualityGuarantee;
            TimeToResult = timeToResult;
        }
    }
}