using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window
    {
        private AppWindow? _appWindow;

        public MainWindow()
        {
            this.InitializeComponent(); // wróci po naprawie XAML
            InitAppWindow();
        }

        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow is not null)
            {
                _appWindow.Title = "GrafikoMat Dy¿urowy 2.0";
                _appWindow.Resize(new SizeInt32(1280, 800));
            }
        }
    }
}
