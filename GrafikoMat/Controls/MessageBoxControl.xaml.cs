using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GrafikoMat.Controls
{
    public sealed partial class MessageBoxControl : UserControl
    {
        public enum MessageType
        {
            Success,
            Error,
            Warning,
            Info
        }

        public MessageBoxControl()
        {
            this.InitializeComponent();
        }

        public void SetMessage(string title, string message, MessageType type)
        {
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;

            switch (type)
            {
                case MessageType.Success:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)); // #10B981
                    StatusIcon.Glyph = "\uF13E"; // Checkmark
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));
                    break;

                case MessageType.Error:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // #EF4444
                    StatusIcon.Glyph = "\uEA39"; // Error
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));
                    break;

                case MessageType.Warning:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36)); // #FBBF24
                    StatusIcon.Glyph = "\uE7BA"; // Warning
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                    break;

                case MessageType.Info:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)); // #3B82F6
                    StatusIcon.Glyph = "\uE946"; // Info
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
                    break;
            }
        }
    }
}