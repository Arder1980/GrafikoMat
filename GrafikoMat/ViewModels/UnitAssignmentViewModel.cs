using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using System;

namespace GrafikoMat.ViewModels
{
    public class UnitAssignmentViewModel : ObservableObject
    {
        public Guid UnitId { get; }
        public string UnitName { get; }
        public string DepartmentName { get; }

        // NOWA WŁAŚCIWOŚĆ: przechowuje informację, czy przypisanie istnieje w bazie
        public bool IsPersisted { get; }

        // NOWA WŁAŚCIWOŚĆ: na jej podstawie UI zablokuje checkbox
        public bool CanBeChanged => !IsPersisted;

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

        // ZMIANA: Zaktualizowany konstruktor przyjmujący nowy parametr
        public UnitAssignmentViewModel(Unit unit, bool isAssigned, bool isActive, bool isPersisted)
        {
            UnitId = unit.Id;
            UnitName = unit.Name;
            DepartmentName = unit.DepartmentName;
            IsPersisted = isPersisted; // Zapamiętujemy stan początkowy
            _isAssigned = isAssigned;
            _isActive = isActive;
        }
    }
}