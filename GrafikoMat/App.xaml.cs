using Microsoft.UI.Xaml;

namespace GrafikoMat
{
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new GrafikoMat.MainWindow();
            MainWindow.Activate();
        }
    }
}