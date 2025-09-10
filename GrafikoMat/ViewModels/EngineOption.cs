using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// Reprezentuje pojedynczy silnik na liście wyboru w UI.
    /// </summary>
    public class EngineOption : ObservableObject
    {
        public SolverType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public string Category { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public EngineOption(SolverType type, string name, string description, string category)
        {
            Type = type;
            Name = name;
            Description = description;
            Category = category;
        }
    }
}