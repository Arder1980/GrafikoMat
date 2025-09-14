using System.Windows.Input;

namespace GrafikoMat.Models
{
    public sealed class UiAction
    {
        public string Label { get; }
        public ICommand Command { get; }
        public bool IsPrimary { get; } // NOWA WŁAŚCIWOŚĆ

        public UiAction(string label, ICommand command, bool isPrimary = false)
        {
            Label = label;
            Command = command;
            IsPrimary = isPrimary;
        }
    }
}