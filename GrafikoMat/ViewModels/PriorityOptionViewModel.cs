using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;
using System.Windows.Input;

namespace GrafikoMat.ViewModels
{
    public class PriorityOptionViewModel : ObservableObject
    {
        public SolverPriority PriorityType { get; }
        public string Name { get; }
        public string Description { get; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isMoveUpEnabled;
        public bool IsMoveUpEnabled
        {
            get => _isMoveUpEnabled;
            set => SetProperty(ref _isMoveUpEnabled, value);
        }

        private bool _isMoveDownEnabled;
        public bool IsMoveDownEnabled
        {
            get => _isMoveDownEnabled;
            set => SetProperty(ref _isMoveDownEnabled, value);
        }

        private int _rank;
        public int Rank
        {
            get => _rank;
            set => SetProperty(ref _rank, value);
        }

        // ZMIANA: Dodano właściwości na komendy
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public PriorityOptionViewModel(SolverPriority priorityType, string name, string description, bool isActive, PrioritiesSettingsViewModel parentViewModel)
        {
            PriorityType = priorityType;
            Name = name;
            Description = description;
            _isActive = isActive;

            // ZMIANA: Komendy w tym obiekcie wywołują metody z nadrzędnego ViewModelu
            MoveUpCommand = new RelayCommand(() => parentViewModel.MoveUpCommand.Execute(this));
            MoveDownCommand = new RelayCommand(() => parentViewModel.MoveDownCommand.Execute(this));
        }
    }
}