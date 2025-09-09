using Microsoft.UI.Xaml;

namespace GrafikoMat
{
    public partial class App : Application
    {
        // ZMIANA: Zamiast generycznego 'Window', tworzymy właściwość
        // o konkretnym typie 'MainWindow', aby uniknąć rzutowania.
        public static MainWindow MainRoot { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Używamy naszej nowej, silnie typowanej właściwości.
            MainRoot = new GrafikoMat.MainWindow();
            MainRoot.Activate();
        }
    }
}