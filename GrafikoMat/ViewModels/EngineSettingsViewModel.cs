using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Services;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class EngineSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

        public ObservableCollection<EngineOption> EngineOptions { get; } = new();
        public ObservableCollection<EngineComparisonInfo> ComparisonData { get; } = new();

        private EngineOption? _selectedEngine;
        public EngineOption? SelectedEngine
        {
            get => _selectedEngine;
            set
            {
                if (SetProperty(ref _selectedEngine, value))
                {
                    foreach (var option in EngineOptions)
                    {
                        option.IsSelected = (option == value);
                    }
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private bool _showStatusMessage;
        public bool ShowStatusMessage { get => _showStatusMessage; set => SetProperty(ref _showStatusMessage, value); }

        public string StatusMessageTitle { get; } = "Sukces";
        public string StatusMessage { get; } = "Ustawienia zostały zapisane.";
        public InfoBarSeverity StatusMessageSeverity { get; } = InfoBarSeverity.Success;

        public IAsyncRelayCommand SaveCommand { get; }

        public EngineSettingsViewModel(SettingsService settingsService, AppSettings appSettings)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;

            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);

            LoadEngineData();
            LoadInitialSelection();
        }

        private void LoadInitialSelection()
        {
            SelectedEngine = EngineOptions.FirstOrDefault(o => o.Type == _appSettings.SelectedSolver)
                             ?? EngineOptions.FirstOrDefault();
        }

        private async Task SaveSettingsAsync()
        {
            if (_selectedEngine == null) return;

            IsLoading = true;
            try
            {
                // Tworzymy nową, zaktualizowaną wersję obiektu ustawień
                _appSettings = _appSettings with { SelectedSolver = _selectedEngine.Type };
                // Zapisujemy ją za pomocą serwisu
                await _settingsService.SaveSettingsAsync(_appSettings);
            }
            finally
            {
                IsLoading = false;
            }

            // Pokaż komunikat o sukcesie
            ShowStatusMessage = true;
            await Task.Delay(2500); // Komunikat widoczny przez 2.5 sekundy
            ShowStatusMessage = false;
        }

        private void LoadEngineData()
        {
            EngineOptions.Add(new EngineOption(SolverType.Backtracking, "BacktrackingSolver",
                "Działa jak skrupulatny detektyw w labiryncie. Systematycznie, krok po kroku, podąża jedną ścieżką, przypisując dyżury dzień po dniu. Gdy trafia w ślepy zaułek (sytuację, w której nie da się obsadzić dyżuru zgodnie z regułami), powraca (backtrack) do ostatniego skrzyżowania i próbuje innej drogi. Gwarantuje znalezienie idealnego rozwiązania, ale dla złożonych problemów jego praca może trwać bardzo długo.",
                "Algorytm z nawrotami"));
            EngineOptions.Add(new EngineOption(SolverType.AStar, "AStarSolver",
                "Można go porównać do doświadczonego nawigatora z mapą i kompasem. W każdym momencie analizuje nie tylko już przebytą drogę, ale również inteligentnie szacuje odległość, jaka jeszcze pozostała do celu. Zamiast ślepo badać wszystkie ścieżki jak Backtracking, A* koncentruje swoje wysiłki na tych, które wydają się najbardziej obiecujące. Dzięki temu potrafi znaleźć optymalną trasę znacznie szybciej.",
                "Wielokryterialny algorytm A*"));
            EngineOptions.Add(new EngineOption(SolverType.Genetic, "GeneticSolver",
                "Inspirowany teorią ewolucji. Algorytm tworzy początkową 'populację' losowych grafików, a następnie poddaje ją procesowi naturalnej selekcji przez wiele pokoleń. Najlepsze grafiki ('osobniki') są ze sobą 'krzyżowane', tworząc nowe 'potomstwo'. Z pokolenia na pokolenie słabsze rozwiązania są eliminowane, a populacja jako całość staje się coraz 'silniejsza', dążąc do wyłonienia niemal idealnego grafiku.",
                "Algorytm genetyczny"));
            EngineOptions.Add(new EngineOption(SolverType.SimulatedAnnealing, "SimulatedAnnealingSolver",
                "Naśladuje proces powolnego studzenia (wyżarzania) metalu, aby uzyskać jego idealną, krystaliczną strukturę. Algorytm zaczyna od wysokiej 'temperatury', na której chętnie akceptuje nawet gorsze modyfikacje grafiku, co pozwala mu na unikanie utknięcia w lokalnych optimach. W miarę jak 'temperatura' opada, algorytm staje się coraz bardziej 'wybredny' i akceptuje już tylko zmiany, które faktycznie poprawiają wynik.",
                "Algorytm symulowanego wyżarzania"));
            EngineOptions.Add(new EngineOption(SolverType.TabuSearch, "TabuSearchSolver",
                "Wyobraź sobie eksploratora, który notuje w dzienniku ostatnio odwiedzone miejsca, by do nich od razu nie wracać. Algorytm w każdym kroku szuka najlepszej modyfikacji grafiku, a żeby uniknąć zapętlenia, prowadzi 'listę tabu' – krótkoterminową pamięć ruchów, które są tymczasowo 'zakazane'. Pozwala to na wydostanie się z lokalnych optimów i zbadanie szerszego obszaru potencjalnych grafików.",
                "Algorytm przeszukiwania z zakazami"));
            EngineOptions.Add(new EngineOption(SolverType.AntColony, "AntColonySolver",
                "Działa w oparciu o obserwację kolonii mrówek. Wiele wirtualnych 'mrówek' jednocześnie buduje kompletne grafiki. Każda mrówka, która stworzy dobry grafik, zostawia cyfrowy 'ślad feromonowy' na fragmentach, z których korzystała. Kolejne 'pokolenia' mrówek są przyciągane do silniejszych śladów, co naturalnie wzmacnia najlepsze elementy i prowadzi całą kolonię do szybkiego znalezienia optymalnego rozwiązania.",
                "Algorytm kolonii mrówek"));
            ComparisonData.Add(new EngineComparisonInfo("BacktrackingSolver", "Przeszukiwanie zupełne", "Deterministyczny", "Nie", "Gwarancja znalezienia wyniku najlepszego z możliwych.", "Od b. krótkiego do astronomicznie długiego", "Niskie"));
            ComparisonData.Add(new EngineComparisonInfo("AStarSolver", "Przeszukiwanie heurystyczne", "Deterministyczny", "Nie", "Wynik bardzo wysokiej jakości (bez gwarancji optimum).", "Krótki / Średni", "Wysokie"));
            ComparisonData.Add(new EngineComparisonInfo("GeneticSolver", "Metaheurystyka ewolucyjna", "Stochastyczny", "Tak", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki", "Średnie / Wysokie"));
            ComparisonData.Add(new EngineComparisonInfo("SimulatedAnnealingSolver", "Metaheurystyka", "Stochastyczny", "Nie", "Wynik zazwyczaj bardzo dobry, lecz bez gwarancji bliskości do wyniku najlepszego z możliwych.", "Średni", "Niskie"));
            ComparisonData.Add(new EngineComparisonInfo("TabuSearchSolver", "Metaheurystyka", "Stochastyczny", "Nie", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki", "Średnie"));
            ComparisonData.Add(new EngineComparisonInfo("AntColonySolver", "Metaheurystyka (inteligencja rozproszona)", "Stochastyczny", "Tak", "Wynik bardzo wysokiej jakości, bliski najlepszemu.", "Krótki", "Wysokie"));
        }
    }
}