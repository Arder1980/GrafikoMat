using Microsoft.UI.Xaml;

namespace GrafikoMat
{
    public partial class App : Application
    {
        // ZMIANA: Dodajemy statyczną właściwość na okno główne
        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // ZMIANA: Przypisujemy instancję okna do naszej statycznej właściwości
            MainWindow = new GrafikoMat.MainWindow();
            MainWindow.Activate();
        }
    }
}