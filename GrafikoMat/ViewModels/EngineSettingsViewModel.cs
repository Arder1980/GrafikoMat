using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Models;
using GrafikoMat.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GrafikoMat.ViewModels
{
    public class EngineSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IUxActionOrchestrator _orchestrator;
        private AppSettings _appSettings;
        private Guid _viewId;
        public ObservableCollection<EngineOption> EngineOptions { get; } = new();

        private EngineOption? _selectedEngine;
        public EngineOption? SelectedEngine
        {
            get => _selectedEngine;
            set
            {
                if (SetProperty(ref _selectedEngine, value))
                {
                    foreach (var option in EngineOptions) { option.IsSelected = (option == value); }
                    OnPropertyChanged(nameof(HasConfigurableParameters)); // ZMIANA: Powiadom o zmianie
                }
            }
        }

        // ZMIANA: Nowa właściwość sterująca widocznością panelu ustawień
        public bool HasConfigurableParameters =>
            SelectedEngine?.Type is SolverType.SimulatedAnnealing or SolverType.Genetic or SolverType.AntColony or SolverType.TabuSearch;

        private double _coolingRateValue;
        public double CoolingRateValue { get => _coolingRateValue; set => SetProperty(ref _coolingRateValue, value); }

        private int _geneticPopulationSizeValue;
        public int GeneticPopulationSizeValue { get => _geneticPopulationSizeValue; set => SetProperty(ref _geneticPopulationSizeValue, value); }

        private int _geneticGenerationsValue;
        public int GeneticGenerationsValue { get => _geneticGenerationsValue; set => SetProperty(ref _geneticGenerationsValue, value); }

        private int _antColonyAntsValue;
        public int AntColonyAntsValue { get => _antColonyAntsValue; set => SetProperty(ref _antColonyAntsValue, value); }

        private int _antColonyGenerationsValue;
        public int AntColonyGenerationsValue { get => _antColonyGenerationsValue; set => SetProperty(ref _antColonyGenerationsValue, value); }

        private int _tabuListSizeValue;
        public int TabuListSizeValue { get => _tabuListSizeValue; set => SetProperty(ref _tabuListSizeValue, value); }

        private int _tabuMaxIterationsValue;
        public int TabuMaxIterationsValue { get => _tabuMaxIterationsValue; set => SetProperty(ref _tabuMaxIterationsValue, value); }

        public IAsyncRelayCommand SaveCommand { get; }

        // ================== NOWE KOMENDY ==================
        public ICommand ResetSimulatedAnnealingCommand { get; }
        public ICommand ResetGeneticCommand { get; }
        public ICommand ResetAntColonyCommand { get; }
        public ICommand ResetTabuSearchCommand { get; }
        // ================================================

        public EngineSettingsViewModel(SettingsService settingsService, AppSettings appSettings)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);

            // ZMIANA: Inicjalizacja komend
            ResetSimulatedAnnealingCommand = new RelayCommand(() => CoolingRateValue = 0.995);
            ResetGeneticCommand = new RelayCommand(() =>
            {
                GeneticPopulationSizeValue = 100;
                GeneticGenerationsValue = 300;
            });
            ResetAntColonyCommand = new RelayCommand(() =>
            {
                AntColonyAntsValue = 75;
                AntColonyGenerationsValue = 300;
            });
            ResetTabuSearchCommand = new RelayCommand(() =>
            {
                TabuListSizeValue = 30;
                TabuMaxIterationsValue = 500;
            });

            LoadEngineData();
            LoadInitialSelection();
        }

        public void SetViewId(Guid viewId) => _viewId = viewId;
        private void LoadInitialSelection()
        {
            SelectedEngine = EngineOptions.FirstOrDefault(o => o.Type == _appSettings.SelectedSolver) ?? EngineOptions.FirstOrDefault();

            CoolingRateValue = _appSettings.CoolingRate;
            GeneticPopulationSizeValue = _appSettings.GeneticPopulationSize;
            GeneticGenerationsValue = _appSettings.GeneticGenerations;
            AntColonyAntsValue = _appSettings.AntColonyAnts;
            AntColonyGenerationsValue = _appSettings.AntColonyGenerations;
            TabuListSizeValue = _appSettings.TabuListSize;
            TabuMaxIterationsValue = _appSettings.TabuMaxIterations;
        }

        private async Task SaveSettingsAsync()
        {
            if (_selectedEngine == null) return;

            var newSettings = _appSettings with
            {
                SelectedSolver = _selectedEngine.Type,
                CoolingRate = this.CoolingRateValue,
                GeneticPopulationSize = this.GeneticPopulationSizeValue,
                GeneticGenerations = this.GeneticGenerationsValue,
                AntColonyAnts = this.AntColonyAntsValue,
                AntColonyGenerations = this.AntColonyGenerationsValue,
                TabuListSize = this.TabuListSizeValue,
                TabuMaxIterations = this.TabuMaxIterationsValue
            };
            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () => await _settingsService.SaveSettingsAsync(newSettings),
                verificationAsync: async () =>
                {
                    var saved = await _settingsService.LoadSettingsAsync(forceReload: true);
                    return saved.SelectedSolver == newSettings.SelectedSolver &&
                           saved.CoolingRate == newSettings.CoolingRate &&
                           saved.GeneticPopulationSize == newSettings.GeneticPopulationSize;
                },
                successMessage: "Nowy silnik obliczeniowy i jego parametry zostały pomyślnie zapisane.",
                errorMessageTitle: "Błąd zapisu ustawień"
            );
            _appSettings = newSettings;

            WeakReferenceMessenger.Default.Send(new SettingsHaveChangedMessage());
        }

        private void LoadEngineData()
        {
            EngineOptions.Clear();
            EngineOptions.Add(new EngineOption(SolverType.Backtracking, "BacktrackingSolver", "Działa jak skrupulatny detektyw w labiryncie. Systematycznie, krok po kroku, podąża jedną ścieżką, przypisując dyżury dzień po dniu. Gdy trafia w ślepy zaułek (sytuację, w której nie da się obsadzić dyżuru zgodnie z regułami), powraca (backtrack) do ostatniego skrzyżowania i próbuje innej drogi. Gwarantuje znalezienie idealnego rozwiązania, ale dla złożonych problemów jego praca może trwać bardzo długo.", "Algorytm z nawrotami", "Przeszukiwanie zupełne", "Deterministyczny", "Nie", "Gwarancja znalezienia wyniku najlepszego z możliwych.", "Od b. krótkiego do astronomicznie długiego"));
            EngineOptions.Add(new EngineOption(SolverType.AStar, "AStarSolver", "Można go porównać do doświadczonego nawigatora z mapą i kompasem. W każdym momencie analizuje nie tylko już przebytą drogę, ale również inteligentnie szacuje odległość, jaka jeszcze pozostała do celu. Zamiast ślepo badać wszystkie ścieżki jak Backtracking, A* koncentruje swoje wysiłki na tych, które wydają się najbardziej obiecujące. Dzięki temu potrafi znaleźć optymalną trasę znacznie szybciej.", "Wielokryterialny algorytm A*", "Przeszukiwanie heurystyczne", "Deterministyczny", "Nie", "Gwarancja znalezienia wyniku optymalnego (przy dopuszczalnej heurystyce).", "Krótki / Średni"));
            EngineOptions.Add(new EngineOption(SolverType.Genetic, "GeneticSolver", "Inspirowany teorią ewolucji. Algorytm tworzy początkową 'populację' losowych grafików, a następnie poddaje ją procesowi naturalnej selekcji przez wiele pokoleń. Najlepsze grafiki ('osobniki') są ze sobą 'krzyżowane', tworząc nowe 'potomstwo'. Z pokolenia na pokolenie słabsze rozwiązania są eliminowane, a populacja jako całość staje się coraz 'silniejsza', dążąc do wyłonienia niemal idealnego grafiku.", "Algorytm genetyczny", "Metaheurystyka", "Stochastyczny", "Tak", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki"));
            EngineOptions.Add(new EngineOption(SolverType.SimulatedAnnealing, "SimulatedAnnealingSolver", "Naśladuje proces powolnego studzenia (wyżarzania) metalu, aby uzyskać jego idealną, krystaliczną strukturę. Algorytm zaczyna od wysokiej 'temperatury', na której chętnie akceptuje nawet gorsze modyfikacje grafiku, co pozwala mu na unikanie utknięcia w lokalnych optimach. W miarę jak 'temperatura' opada, algorytm staje się coraz bardziej 'wybredny' i akceptuje już tylko zmiany, które faktycznie poprawiają wynik.", "Algorytm symulowanego wyżarzania", "Metaheurystyka", "Stochastyczny", "Nie", "Wynik dobrej jakości, silnie zależny od parametrów wyżarzania.", "Średni"));
            EngineOptions.Add(new EngineOption(SolverType.TabuSearch, "TabuSearchSolver", "Wyobraź sobie eksploratora, który notuje w dzienniku ostatnio odwiedzone miejsca, by do nich od razu nie wracać. Algorytm w każdym kroku szuka najlepszej modyfikacji grafiku, a żeby uniknąć zapętlenia, prowadzi 'listę tabu' – krótkoterminową pamięć ruchów, które są tymczasowo 'zakazane'. Pozwala to na wydostanie się z lokalnych optimów i zbadanie szerszego obszaru potencjalnych grafików.", "Algorytm przeszukiwania z zakazami", "Metaheurystyka", "Stochastyczny", "Nie", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki"));
            EngineOptions.Add(new EngineOption(SolverType.AntColony, "AntColonySolver", "Działa w oparciu o obserwację kolonii mrówek. Wiele wirtualnych 'mrówek' jednocześnie buduje kompletne grafiki. Każda mrówka, która stworzy dobry grafik, zostawia cyfrowy 'ślad feromonowy' na fragmentach, z których korzystała. Kolejne 'pokolenia' mrówek są przyciągane do silniejszych śladów, co naturalnie wzmacnia najlepsze elementy i prowadzi całą kolonię do szybkiego znalezienia optymalnego rozwiązania.", "Algorytm kolonii mrówek", "Metaheurystyka", "Stochastyczny", "Tak", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki"));
        }
    }
}