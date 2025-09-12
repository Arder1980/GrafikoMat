using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class PrioritiesSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IUxActionOrchestrator _orchestrator;
        private readonly DispatcherQueue? _dispatcher;
        private AppSettings _appSettings;
        private Guid _viewId;

        public ObservableCollection<PriorityOptionViewModel> ActivePriorities { get; } = new();
        public ObservableCollection<PriorityOptionViewModel> InactivePriorities { get; } = new();

        public IRelayCommand<PriorityOptionViewModel> MoveUpCommand { get; }
        public IRelayCommand<PriorityOptionViewModel> MoveDownCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }

        public PrioritiesSettingsViewModel(SettingsService settingsService, AppSettings appSettings, DispatcherQueue? dispatcher)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            _dispatcher = dispatcher;

            MoveUpCommand = new RelayCommand<PriorityOptionViewModel>(MoveUp);
            MoveDownCommand = new RelayCommand<PriorityOptionViewModel>(MoveDown);
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);

            LoadPriorities();
        }

        public void SetViewId(Guid viewId) => _viewId = viewId;

        private void LoadPriorities()
        {
            ActivePriorities.Clear();
            InactivePriorities.Clear();
            var descriptions = GetPriorityDescriptions();

            foreach (var setting in _appSettings.Priorities.OrderBy(p => p.Priority.ToString()))
            {
                // ZMIANA: Dodano brakujący argument 'this' do konstruktora
                var vm = new PriorityOptionViewModel(
                    setting.Priority,
                    descriptions[setting.Priority].Name,
                    descriptions[setting.Priority].Description,
                    setting.IsActive,
                    this
                );
                vm.PropertyChanged += OnPriorityPropertyChanged;

                if (vm.IsActive)
                {
                    ActivePriorities.Add(vm);
                }
                else
                {
                    InactivePriorities.Add(vm);
                }
            }

            var orderedActive = _appSettings.Priorities
                .Where(p => p.IsActive)
                .Select(p => ActivePriorities.First(vm => vm.PriorityType == p.Priority))
                .ToList();

            ActivePriorities.Clear();
            orderedActive.ForEach(p => ActivePriorities.Add(p));

            RefreshListState();
        }

        private void OnPriorityPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PriorityOptionViewModel.IsActive) && sender is PriorityOptionViewModel changedPriority)
            {
                if (changedPriority.IsActive)
                {
                    if (InactivePriorities.Remove(changedPriority))
                    {
                        ActivePriorities.Add(changedPriority);
                    }
                }
                else
                {
                    if (ActivePriorities.Remove(changedPriority))
                    {
                        InactivePriorities.Add(changedPriority);
                        var sortedInactive = InactivePriorities.OrderBy(p => p.Name).ToList();
                        InactivePriorities.Clear();
                        sortedInactive.ForEach(p => InactivePriorities.Add(p));
                    }
                }
                RefreshListState();
            }
        }

        private void MoveUp(PriorityOptionViewModel? priority)
        {
            if (priority == null) return;
            int index = ActivePriorities.IndexOf(priority);
            if (index > 0)
            {
                ActivePriorities.Move(index, index - 1);
                _dispatcher?.TryEnqueue(RefreshListState);
            }
        }

        private void MoveDown(PriorityOptionViewModel? priority)
        {
            if (priority == null) return;
            int index = ActivePriorities.IndexOf(priority);
            if (index < ActivePriorities.Count - 1)
            {
                ActivePriorities.Move(index, index + 1);
                _dispatcher?.TryEnqueue(RefreshListState);
            }
        }

        public void RefreshListState()
        {
            for (int i = 0; i < ActivePriorities.Count; i++)
            {
                var item = ActivePriorities[i];
                item.Rank = i + 1;
                item.IsMoveUpEnabled = (i > 0);
                item.IsMoveDownEnabled = (i < ActivePriorities.Count - 1);
            }

            foreach (var item in InactivePriorities)
            {
                item.Rank = 0;
            }
        }

        private async Task SaveSettingsAsync()
        {
            var finalPriorityList = ActivePriorities
                .Concat(InactivePriorities)
                .Select(p => new PrioritySetting(p.PriorityType, p.IsActive))
                .ToList();

            var newSettings = _appSettings with { Priorities = finalPriorityList };

            await _orchestrator.PerformActionAsync(
                viewId: _viewId,
                actionAsync: async () => await _settingsService.SaveSettingsAsync(newSettings),
                verificationAsync: async () =>
                {
                    var saved = await _settingsService.LoadSettingsAsync(forceReload: true);
                    return saved.Priorities.SequenceEqual(newSettings.Priorities);
                },
                successMessage: "Nowa kolejność priorytetów została pomyślnie zapisana.",
                errorMessageTitle: "Błąd zapisu ustawień"
            );
            _appSettings = newSettings;
        }

        private Dictionary<SolverPriority, (string Name, string Description)> GetPriorityDescriptions()
        {
            return new()
            {
                [SolverPriority.InitialContinuity] = ("Ciągłość grafiku od początku miesiąca", "Najważniejszy priorytet bezpieczeństwa. Silnik za wszelką cenę stara się obsadzić nieprzerwany ciąg dyżurów od pierwszego dnia miesiąca. Nieobsadzone dni są 'wypychane' na koniec, co daje czas na znalezienie zastępstw i ręczne uzupełnienie grafiku."),
                [SolverPriority.TotalAssignments] = ("Maksymalna obsada", "Priorytet 'ilościowy'. Silnik dąży do obsadzenia jak największej liczby dyżurów w całym miesiącu, nawet jeśli oznacza to nierówne obciążenie lekarzy lub gorsze dopasowanie do ich preferencji. Cel: jak najmniej 'dziur' w grafiku."),
                [SolverPriority.Fairness] = ("Sprawiedliwość obciążenia", "Dba o 'spokój w zespole'. Silnik stara się, aby każdy lekarz zrealizował podobny procent swojego zadeklarowanego limitu dyżurów. Zapobiega sytuacjom, w których jedna osoba ma znacznie więcej dyżurów niż inne, co jest kluczowe dla utrzymania morale."),
                [SolverPriority.Spacing] = ("Równomierne rozłożenie dyżurów", "Priorytet 'work-life balance'. Zamiast skupiać dyżury jednego lekarza w krótkim okresie, silnik stara się rozłożyć je jak najbardziej równomiernie w ciągu miesiąca. Zapobiega to 'seriom' dyżurów i pozwala na regularny odpoczynek."),
                [SolverPriority.DeclarationCompliance] = ("Zgodność z preferencjami", "Stara się, aby każdy był zadowolony. Silnik preferuje przydzielanie dyżurów lekarzom, którzy zadeklarowali 'Chcę', przed tymi, którzy zadeklarowali tylko 'Mogę'. Wysoka pozycja tego priorytetu zwiększa satysfakcję zespołu.")
            };
        }
    }
}