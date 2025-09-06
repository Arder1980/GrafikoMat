using GrafikoMat.Common;
using GrafikoMat.Core.Data; // <-- Dodana dyrektywa
using System;

namespace GrafikoMat.ViewModels
{
    public class UnitAssignmentViewModel : ObservableObject
    {
        public Guid UnitId { get; }
        public string UnitName { get; }

        private bool _isAssigned;
        public bool IsAssigned
        {
            get => _isAssigned;
            set => SetProperty(ref _isAssigned, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public UnitAssignmentViewModel(Unit unit, bool isAssigned, bool isActive)
        {
            UnitId = unit.Id;
            UnitName = unit.Name;
            _isAssigned = isAssigned;
            _isActive = isActive;
        }
    }
}