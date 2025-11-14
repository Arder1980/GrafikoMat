using CommunityToolkit.Mvvm.ComponentModel;

namespace GrafikoMat.ViewModels
{
    /// <summary>
    /// ViewModel dla pojedynczej komórki kalendarza w widoku Dashboard.
    /// </summary>
    public partial class CalendarCellViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _row;

        [ObservableProperty]
        private int _column;

        [ObservableProperty]
        private string _text = string.Empty;

        [ObservableProperty]
        private string _backgroundColor = "#00000000"; // Transparent

        [ObservableProperty]
        private string _borderColor = "#18000000";

        [ObservableProperty]
        private string _borderThickness = "0,0,1,1";

        [ObservableProperty]
        private bool _isBold;

        [ObservableProperty]
        private double _opacity = 1.0;

        [ObservableProperty]
        private int _fontSize = 14;

        [ObservableProperty]
        private string _textAlignment = "Center";

        [ObservableProperty]
        private string _verticalAlignment = "Center";

        [ObservableProperty]
        private bool _isHeader;

        [ObservableProperty]
        private double _lineHeight = 14;

        [ObservableProperty]
        private string _padding = "0";
    }
}
