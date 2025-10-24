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
        private bool _isDirty = false;
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
                    OnPropertyChanged(nameof(HasConfigurableParameters));
                    MarkAsDirty();
                }
            }
        }

        public bool HasConfigurableParameters =>
            SelectedEngine != null &&
            SelectedEngine.Type != SolverType.Backtracking &&
            SelectedEngine.Type != SolverType.AStar;

        // === TIMEOUT ===
        private int _timeoutMinutesValue;
        public int TimeoutMinutesValue { get => _timeoutMinutesValue; set { if (SetProperty(ref _timeoutMinutesValue, value)) MarkAsDirty(); } }
        public int TimeoutMinutesMin => SolverDefaults.TimeoutMinutes.Min;
        public int TimeoutMinutesMax => SolverDefaults.TimeoutMinutes.Max;
        public int TimeoutMinutesStep => SolverDefaults.TimeoutMinutes.Step;

        // === SIMULATED ANNEALING ===
        private double _coolingRateValue;
        public double CoolingRateValue { get => _coolingRateValue; set { if (SetProperty(ref _coolingRateValue, value)) MarkAsDirty(); } }
        public double CoolingRateMin => SolverDefaults.CoolingRate.Min;
        public double CoolingRateMax => SolverDefaults.CoolingRate.Max;
        public double CoolingRateStep => SolverDefaults.CoolingRate.Step;

        // === GENETIC ===
        private int _geneticPopulationSizeValue;
        public int GeneticPopulationSizeValue { get => _geneticPopulationSizeValue; set { if (SetProperty(ref _geneticPopulationSizeValue, value)) MarkAsDirty(); } }
        public int GeneticPopulationSizeMin => SolverDefaults.GeneticPopulationSize.Min;
        public int GeneticPopulationSizeMax => SolverDefaults.GeneticPopulationSize.Max;
        public int GeneticPopulationSizeStep => SolverDefaults.GeneticPopulationSize.Step;

        private int _geneticGenerationsValue;
        public int GeneticGenerationsValue { get => _geneticGenerationsValue; set { if (SetProperty(ref _geneticGenerationsValue, value)) MarkAsDirty(); } }
        public int GeneticGenerationsMin => SolverDefaults.GeneticGenerations.Min;
        public int GeneticGenerationsMax => SolverDefaults.GeneticGenerations.Max;
        public int GeneticGenerationsStep => SolverDefaults.GeneticGenerations.Step;

        // === ANT COLONY ===
        private int _antColonyAntsValue;
        public int AntColonyAntsValue { get => _antColonyAntsValue; set { if (SetProperty(ref _antColonyAntsValue, value)) MarkAsDirty(); } }
        public int AntColonyAntsMin => SolverDefaults.AntColonyAnts.Min;
        public int AntColonyAntsMax => SolverDefaults.AntColonyAnts.Max;
        public int AntColonyAntsStep => SolverDefaults.AntColonyAnts.Step;

        private int _antColonyGenerationsValue;
        public int AntColonyGenerationsValue { get => _antColonyGenerationsValue; set { if (SetProperty(ref _antColonyGenerationsValue, value)) MarkAsDirty(); } }
        public int AntColonyGenerationsMin => SolverDefaults.AntColonyGenerations.Min;
        public int AntColonyGenerationsMax => SolverDefaults.AntColonyGenerations.Max;
        public int AntColonyGenerationsStep => SolverDefaults.AntColonyGenerations.Step;

        // === TABU SEARCH ===
        private int _tabuListSizeValue;
        public int TabuListSizeValue { get => _tabuListSizeValue; set { if (SetProperty(ref _tabuListSizeValue, value)) MarkAsDirty(); } }
        public int TabuListSizeMin => SolverDefaults.TabuListSize.Min;
        public int TabuListSizeMax => SolverDefaults.TabuListSize.Max;
        public int TabuListSizeStep => SolverDefaults.TabuListSize.Step;

        private int _tabuMaxIterationsValue;
        public int TabuMaxIterationsValue { get => _tabuMaxIterationsValue; set { if (SetProperty(ref _tabuMaxIterationsValue, value)) MarkAsDirty(); } }
        public int TabuMaxIterationsMin => SolverDefaults.TabuMaxIterations.Min;
        public int TabuMaxIterationsMax => SolverDefaults.TabuMaxIterations.Max;
        public int TabuMaxIterationsStep => SolverDefaults.TabuMaxIterations.Step;

        public IAsyncRelayCommand SaveCommand { get; }
        public ICommand ResetSimulatedAnnealingCommand { get; }
        public ICommand ResetGeneticCommand { get; }
        public ICommand ResetAntColonyCommand { get; }
        public ICommand ResetTabuSearchCommand { get; }

        public EngineSettingsViewModel(SettingsService settingsService, AppSettings appSettings)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync, () => _isDirty);

            ResetSimulatedAnnealingCommand = new RelayCommand(() =>
                CoolingRateValue = SolverDefaults.CoolingRate.Default);

            ResetGeneticCommand = new RelayCommand(() =>
            {
                GeneticPopulationSizeValue = SolverDefaults.GeneticPopulationSize.Default;
                GeneticGenerationsValue = SolverDefaults.GeneticGenerations.Default;
            });

            ResetAntColonyCommand = new RelayCommand(() =>
            {
                AntColonyAntsValue = SolverDefaults.AntColonyAnts.Default;
                AntColonyGenerationsValue = SolverDefaults.AntColonyGenerations.Default;
            });

            ResetTabuSearchCommand = new RelayCommand(() =>
            {
                TabuListSizeValue = SolverDefaults.TabuListSize.Default;
                TabuMaxIterationsValue = SolverDefaults.TabuMaxIterations.Default;
            });

            LoadEngineData();
            LoadInitialSelection();
        }

        private void MarkAsDirty()
        {
            _isDirty = true;
            SaveCommand.NotifyCanExecuteChanged();
        }

        public void SetViewId(Guid viewId) => _viewId = viewId;

        private void LoadInitialSelection()
        {
            SelectedEngine = EngineOptions.FirstOrDefault(o => o.Type == _appSettings.SelectedSolver)
                ?? EngineOptions.FirstOrDefault();

            TimeoutMinutesValue = _appSettings.TimeoutMinutes;
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
                TimeoutMinutes = this.TimeoutMinutesValue,
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
                           saved.TimeoutMinutes == newSettings.TimeoutMinutes &&
                           saved.CoolingRate == newSettings.CoolingRate &&
                           saved.GeneticPopulationSize == newSettings.GeneticPopulationSize;
                },
                successMessage: "Nowy silnik obliczeniowy i jego parametry zostały pomyślnie zapisane.",
                errorMessageTitle: "Błąd zapisu ustawień"
            );

            _appSettings = newSettings;
            _isDirty = false;
            SaveCommand.NotifyCanExecuteChanged();
            WeakReferenceMessenger.Default.Send(new SettingsHaveChangedMessage());
        }

        private void LoadEngineData()
        {
            EngineOptions.Clear();

            // Backtracking
            EngineOptions.Add(new EngineOption(
                SolverType.Backtracking,
                "BacktrackingSolver (algorytm z nawrotami)",
                "Wyobraź sobie skrupulatnego bibliotekarza, który musi ułożyć książki na półkach według bardzo ścisłych reguł. Zaczyna od pierwszej półki i pierwszej książki, stawia ją, a następnie bierze drugą i sprawdza, czy pasuje obok. Jeśli tak, kontynuuje. Jeśli jednak dojdzie do momentu, w którym żadna z pozostałych książek nie pasuje, cofa się (stąd 'backtrack' - nawrót), zabiera ostatnio postawioną książkę i próbuje na jej miejsce postawić inną. Powtarza ten proces, systematycznie sprawdzając każdą możliwą kombinację, aż znajdzie idealne ułożenie lub wyczerpie wszystkie możliwości. Jest to metoda gwarantująca znalezienie najlepszego rozwiązania, ale jej koszt czasowy może być ogromny, jeśli problem ma wiele możliwych kombinacji (jak grafik z wieloma lekarzami i dniami).",
                "Algorytm z nawrotami",
                "Przeszukiwanie zupełne",
                "Deterministyczny",
                "Nie",
                "Gwarancja znalezienia wyniku najlepszego z możliwych.",
                "Od b. krótkiego do astronomicznie długiego"));

            // A*
            EngineOptions.Add(new EngineOption(
                SolverType.AStar,
                "AStarSolver (algorytm A*)",
                "Ten algorytm działa jak zaawansowany system nawigacji GPS w mieście z niezliczoną ilością dróg. Zamiast jechać na ślepo, A* w każdym momencie analizuje dwie rzeczy: koszt już przebytej trasy (ile dyżurów już przypisano i jak dobrze) oraz szacowany koszt dotarcia do celu (jak 'obiecujące' są pozostałe dni do obsadzenia). Dzięki tej heurystycznej ocenie przyszłości, algorytm inteligentnie wybiera najbardziej obiecujące ścieżki, odcinając te, które już na wczesnym etapie wydają się prowadzić do gorszego wyniku. To sprawia, że jest znacznie wydajniejszy od czystego backtrackingu, zachowując przy tym zdolność do znalezienia optymalnego rozwiązania. W przeciwieństwie do metaheurystyk, A* jest deterministyczny i jednowątkowy, ale jego przeszukiwanie jest kierowane heurystyką, co drastycznie redukuje przestrzeń poszukiwań.",
                "Wielokryterialny algorytm A*",
                "Przeszukiwanie heurystyczne",
                "Deterministyczny",
                "Nie",
                "Gwarancja znalezienia wyniku optymalnego (przy dopuszczalnej heurystyce).",
                "Krótki / Średni"));

            // Genetic
            EngineOptions.Add(new EngineOption(
                SolverType.Genetic,
                "GeneticSolver (algorytm genetyczny)",
                "Działa na zasadach ewolucji biologicznej. Na początku tworzy dużą 'populację' całkowicie losowych grafików. Następnie ocenia 'przystosowanie' każdego z nich – jak dobrze spełnia założone kryteria. Najlepsze grafiki ('osobniki') są wybierane do 'rozmnażania': ich fragmenty są ze sobą mieszane (krzyżowanie), tworząc nowe 'potomstwo' dziedziczące cechy po 'rodzicach'. Dodatkowo wprowadzane są losowe 'mutacje' (np. zmiana lekarza w jednym dniu), aby zwiększyć różnorodność. Z pokolenia na pokolenie słabe rozwiązania wymierają, a cała populacja ewoluuje w kierunku rozwiązań o bardzo wysokiej jakości. To potężna, wielowątkowa technika stosująca równoległą ewaluację populacji, idealna do złożonych problemów optymalizacyjnych.",
                "Algorytm genetyczny",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik bardzo wysokiej jakości, bliski najlepszemu.",
                "Krótki"));

            // Simulated Annealing
            EngineOptions.Add(new EngineOption(
                SolverType.SimulatedAnnealing,
                "SimulatedAnnealingSolver (algorytm symulowanego wyżarzania)",
                "Naśladuje proces wyżarzania stali w hucie. Metal jest najpierw podgrzewany do bardzo wysokiej temperatury, co pozwala atomom na swobodne przemieszczanie się, a następnie jest bardzo powoli schładzany, aby atomy mogły ułożyć się w idealnie uporządkowaną strukturę krystaliczną. W algorytmie 'temperatura' to prawdopodobieństwo akceptacji gorszego rozwiązania. Na początku, przy wysokiej temperaturze, algorytm chętnie akceptuje nawet zmiany pogarszające wynik, co pozwala mu 'wyskakiwać' z lokalnych optimów i eksplorować całą przestrzeń rozwiązań. W miarę jak temperatura spada, staje się coraz bardziej wybredny, akceptując już tylko te zmiany, które faktycznie poprawiają grafik. Tempo schładzania jest kluczowym parametrem równoważącym jakość i czas obliczeń.",
                "Algorytm symulowanego wyżarzania",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik dobrej jakości, silnie zależny od tempa schładzania.",
                "Średni"));

            // Tabu Search
            EngineOptions.Add(new EngineOption(
                SolverType.TabuSearch,
                "TabuSearchSolver (algorytm przeszukiwania z zabronieniami)",
                "To jak eksploracja skomplikowanego labiryntu z mapą odwiedzonych miejsc. W każdym ruchu algorytm rozważa wszystkie możliwe 'posunięcia' (np. zamianę lekarza w danym dniu) i wybiera najbardziej obiecujące, nawet jeśli chwilowo pogarsza to wynik. Kluczowym elementem jest 'lista tabu' – krótka pamięć ostatnio wykonanych ruchów, jak zamykanie przebytych korytarzy. Jeśli algorytm właśnie zamienił lekarza A na B, to cofnięcie tej zamiany (B na A) staje się na pewien czas 'tabu' (zakazane). Ta prosta zasada zapobiega zapętleniu się algorytmu i utknięciu w lokalnym optimum, zmuszając go do systematycznej eksploracji nowych, nieodwiedzonych jeszcze rejonów przestrzeni rozwiązań. Algorytm wykorzystuje wielowątkową równoległą ewaluację sąsiadów, dzięki czemu jest bardzo skuteczny w znajdowaniu wysokiej jakości wyników.",
                "Algorytm przeszukiwania z zabronieniami",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik bardzo wysokiej jakości, zbliżony do najlepszego.",
                "Krótki"));

            // Ant Colony
            EngineOptions.Add(new EngineOption(
                SolverType.AntColony,
                "AntColonySolver (algorytm kolonii mrówek)",
                "Algorytm inspirowany sposobem, w jaki mrówki znajdują najkrótsze trasy do źródeł pożywienia. Każda 'mrówka' (agent) próbuje zbudować kompletny grafik, poruszając się dzień po dniu i wybierając lekarzy. Wybory nie są losowe: mrówki kierują się śladami feromonowymi – cyfrowymi znacznikami pozostawianymi przez poprzednie mrówki. Im lepszy był grafik, tym więcej feromonu zostawiono na jego 'ścieżce'. Nowe mrówki są więc naturalnie przyciągane do sprawdzonych, obiecujących decyzji, zachowując jednak pewną losowość pozwalającą odkrywać nowe możliwości. Z czasem feromony częściowo parują, więc stare, słabe ścieżki są zapominane. Efekt? Kolonia stopniowo konwerguje wokół najlepszych strategii, tworząc wysokiej jakości rozwiązania dzięki współpracy wielu niezależnych agentów działających równolegle.",
                "Algorytm kolonii mrówek",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik wysokiej jakości, zbliżony do najlepszego.",
                "Krótki"));
        }
    }
}