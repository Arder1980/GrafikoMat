using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;

namespace GrafikoMat
{
    public partial class App : Application
    {
        public static MainWindow MainRoot { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // ----- POCZĄTEK NOWEGO KODU -----
            // Tworzymy i rejestrujemy orkiestratora, aby był dostępny w całej aplikacji.
            // Będzie on używał domyślnej instancji Messengera z CommunityToolkit.
            var orchestrator = new UxActionOrchestrator(WeakReferenceMessenger.Default);
            ServiceProvider.Register<IUxActionOrchestrator>(orchestrator);
            // ------ KONIEC NOWEGO KODU ------

            MainRoot = new GrafikoMat.MainWindow();
            MainRoot.Activate();
        }
    }
}