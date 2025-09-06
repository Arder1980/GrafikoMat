using System.Windows.Input;

namespace GrafikoMat.Models
{
    /// <summary>
    /// Reprezentuje pojedynczą akcję (przycisk) w interfejsie użytkownika.
    /// </summary>
    public sealed class UiAction
    {
        public string Label { get; }
        public ICommand Command { get; }

        public UiAction(string label, ICommand command)
        {
            Label = label;
            Command = command;
        }
    }
}