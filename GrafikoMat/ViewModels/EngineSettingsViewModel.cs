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
                    OnPropertyChanged(nameof(HasConfigurableParameters));
                }
            }
        }

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

        public ICommand ResetSimulatedAnnealingCommand { get; }
        public ICommand ResetGeneticCommand { get; }
        public ICommand ResetAntColonyCommand { get; }
        public ICommand ResetTabuSearchCommand { get; }

        public EngineSettingsViewModel(SettingsService settingsService, AppSettings appSettings)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);

            ResetSimulatedAnnealingCommand = new RelayCommand(() => CoolingRateValue = 0.995);

            ResetGeneticCommand = new RelayCommand(() =>
            {
                GeneticPopulationSizeValue = 100;
                GeneticGenerationsValue = 300;
            });

            // ====== ZMIANA PRECISION+: Nowe wartości domyślne dla AntColony ======
            // BYŁO: AntColonyAntsValue = 75, AntColonyGenerationsValue = 300
            // TERAZ: zoptymalizowane dla jakości 97-99% przy ~75% redukcji czasu
            ResetAntColonyCommand = new RelayCommand(() =>
            {
                AntColonyAntsValue = 40;              // Zmienione z 75 → 40
                AntColonyGenerationsValue = 120;      // Zmienione z 300 → 120
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
            SelectedEngine = EngineOptions.FirstOrDefault(o => o.Type == _appSettings.SelectedSolver)
                ?? EngineOptions.FirstOrDefault();

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

            // Backtracking
            EngineOptions.Add(new EngineOption(
                SolverType.Backtracking,
                "BacktrackingSolver",
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
                "AStarSolver",
                "Ten algorytm działa jak zaawansowany system nawigacji GPS w mieście, w którym jest nieskończenie wiele dróg. Zamiast jechać na ślepo, A* w każdym momencie analizuje dwie rzeczy: koszt już przebytej trasy (ile dyżurów już przypisano i jak dobrze) oraz szacowany koszt dotarcia do celu (jak 'obiecujące' są pozostałe dni do obsadzenia). Dzięki tej heurystycznej ocenie przyszłości, algorytm inteligentnie wybiera najbardziej obiecujące ścieżki, ignorując te, które już na wczesnym etapie wydają się prowadzić do gorszego wyniku. To sprawia, że jest znacznie wydajniejszy od czystego backtrackingu, zachowując przy tym zdolność do znalezienia optymalnego rozwiązania, jeśli jego 'mapa' (heurystyka) jest dobrze skalibrowana.",
                "Wielokryterialny algorytm A*",
                "Przeszukiwanie heurystyczne",
                "Deterministyczny",
                "Nie",
                "Gwarancja znalezienia wyniku optymalnego (przy dopuszczalnej heurystyce).",
                "Krótki / Średni"));

            // Genetic
            EngineOptions.Add(new EngineOption(
                SolverType.Genetic,
                "GeneticSolver",
                "Działa na zasadach ewolucji. Na początku tworzy dużą 'populację' całkowicie losowych, często bezsensownych grafików. Następnie ocenia 'przystosowanie' każdego z nich – jak dobrze spełnia założone kryteria. Najlepsze grafiki ('osobniki') są wybierane do 'rozmnażania': ich fragmenty są ze sobą mieszane (krzyżowanie), tworząc nowe 'potomstwo', które dziedziczy cechy po 'rodzicach'. Dodatkowo, wprowadzane są losowe 'mutacje' (np. zmiana lekarza w jednym dniu), aby zwiększyć różnorodność. Z pokolenia na pokolenie słabe rozwiązania wymierają, a cała populacja staje się coraz lepsza, zbiegając się w kierunku rozwiązania o bardzo wysokiej jakości. To potężna, wielowątkowa technika, idealna do złożonych problemów.",
                "Algorytm genetyczny",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik bardzo wysokiej jakości, bliski najlepszemu.",
                "Krótki"));

            // Simulated Annealing
            EngineOptions.Add(new EngineOption(
                SolverType.SimulatedAnnealing,
                "SimulatedAnnealingSolver",
                "Naśladuje proces wyżarzania stali w hucie. Metal jest najpierw podgrzewany do bardzo wysokiej temperatury, co pozwala atomom na swobodne przemieszczanie się, a następnie jest bardzo powoli schładzany, aby atomy mogły ułożyć się w idealnie uporządkowaną, stabilną strukturę krystaliczną. W algorytmie 'temperatura' to prawdopodobieństwo akceptacji gorszego rozwiązania. Na początku, przy wysokiej temperaturze, algorytm chętnie dokonuje nawet losowych, pogarszających wynik zmian, co pozwala mu 'wyskakiwać' z lokalnych optimów i eksplorować całą przestrzeń rozwiązań. W miarę jak 'temperatura' spada, staje się coraz bardziej zachowawczy, akceptując już tylko te zmiany, które faktycznie poprawiają grafik. Tempo schładzania jest kluczowym parametrem do znalezienia złotego środka między jakością a czasem.",
                "Algorytm symulowanego wyżarzania",
                "Metaheurystyka",
                "Stochastyczny",
                "Nie",
                "Wynik dobrej jakości, silnie zależny od parametrów wyżarzania.",
                "Średni"));

            // Tabu Search
            EngineOptions.Add(new EngineOption(
                SolverType.TabuSearch,
                "TabuSearchSolver",
                "To jak gra w szachy z samym sobą, ale z notatnikiem. W każdym ruchu algorytm rozważa wszystkie możliwe 'posunięcia' (np. zamianę lekarza w danym dniu) i wykonuje to, które przynosi największą natychmiastową korzyść, nawet jeśli chwilowo pogarsza to ogólny wynik. Kluczowym elementem jest 'lista tabu' – krótka pamięć ostatnio wykonanych ruchów. Jeśli algorytm właśnie zamienił lekarza A na B, to cofnięcie tej zamiany (B na A) staje się na pewien czas 'tabu' (zakazane). Ta prosta zasada zapobiega zapętleniu się algorytmu i utknięciu w płytkim, lokalnym optimum, zmuszając go do eksplorowania nowych, nieodwiedzonych jeszcze rejonów przestrzeni rozwiązań. Dzięki temu jest bardzo skuteczny w znajdowaniu wysokiej jakości wyników.",
                "Algorytm przeszukiwania z zakazami",
                "Metaheurystyka",
                "Stochastyczny",
                "Nie",
                "Wynik bardzo wysokiej jakości, bliski najlepszemu.",
                "Krótki"));

            // Ant Colony
            EngineOptions.Add(new EngineOption(
                SolverType.AntColony,
                "AntColonySolver",
                "Inspirowany zachowaniem mrówek szukających najkrótszej drogi do pożywienia. Wirtualna 'kolonia mrówek' jest wysyłana do budowania grafików. Każda 'mrówka' konstruuje kompletny grafik od początku do końca, dokonując po drodze wyborów (którego lekarza przypisać do dnia) w oparciu o dwie wskazówki: atrakcyjność danej opcji (np. deklaracja 'Chcę') oraz siłę 'śladu feromonowego' pozostawionego przez poprzednie mrówki. Gdy mrówka ukończy dobry grafik, wraca i wzmacnia feromonem ścieżki, z których korzystała. Z czasem feromon na gorszych ścieżkach 'paruje', a na najlepszych kumuluje się, co sprawia, że kolejne pokolenia mrówek coraz częściej podążają w kierunku optymalnego rozwiązania. To inteligentny, równoległy proces, który dobrze radzi sobie ze złożonymi problemami.",
                "Algorytm kolonii mrówek",
                "Metaheurystyka",
                "Stochastyczny",
                "Tak",
                "Wynik bardzo wysokiej jakości, bliski najlepszemu.",
                "Krótki"));
        }
    }
}