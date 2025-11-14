using GrafikoMat.Core.Data;
using GrafikoMat.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using SlotPart = GrafikoMat.Core.Enums.SlotPart;
using CoDutyStatus = GrafikoMat.Core.Enums.CoDutyStatus;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using WinUIBrush = Microsoft.UI.Xaml.Media.Brush;
using WinUISolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinUIColor = Windows.UI.Color;
using WinUIColors = Microsoft.UI.Colors;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action? SaveAndCloseRequested;
        public event Action? CloseRequested;

        /// <summary>
        /// Zamyka widok deklaracji z opcjonalnym sprawdzeniem niezapisanych zmian.
        /// </summary>
        /// <param name="skipChangeCheck">Jeśli true, pomija sprawdzanie zmian i zamyka natychmiast</param>
        public async void OnDeclCloseOnly(bool skipChangeCheck = false)
        {
            // Jeśli skipChangeCheck=true, zamknij bez sprawdzania (użytkownik już podjął decyzję)
            if (skipChangeCheck)
            {
                CloseRequested?.Invoke();
                return;
            }

            // Sprawdź czy są niezapisane zmiany
            if (ViewModel != null && ViewModel.HasUnsavedChanges)
            {
                var dialog = App.CreateThemedDialog();
                dialog.Title = "Niezapisane zmiany";
                dialog.Content = "Masz niezapisane zmiany. Co chcesz zrobić?";
                dialog.PrimaryButtonText = "Odrzuć zmiany i wyjdź";
                dialog.SecondaryButtonText = "Zapisz i wyjdź";
                dialog.CloseButtonText = "Anuluj";
                dialog.DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Secondary; // Zapisz jako domyślne (bezpieczniejsze)

                var result = await dialog.ShowAsync();

                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    // Użytkownik odrzuca zmiany - zamknij bez zapisu
                    CloseRequested?.Invoke();
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
                {
                    // Użytkownik chce zapisać
                    await ViewModel.SaveAsyncCommand.ExecuteAsync(null);
                    CloseRequested?.Invoke();
                }
                // Jeśli Close (Anuluj) - nie rób nic
            }
            else
            {
                // Brak niezapisanych zmian - zamknij od razu
                CloseRequested?.Invoke();
            }
        }

        public void OnDeclSaveAndCloseOnly() => SaveAndCloseRequested?.Invoke();

        public DeclarationsViewModel ViewModel => this.DataContext as DeclarationsViewModel;

        private void SubscribeToViewModelEvents()
        {
            if (ViewModel != null)
            {
                ViewModel.DeclarationsSaved += OnDeclarationsSaved;
            }
        }

        private void UnsubscribeFromViewModelEvents()
        {
            if (ViewModel != null)
            {
                ViewModel.DeclarationsSaved -= OnDeclarationsSaved;
            }
        }

        private async void OnDeclarationsSaved(object? sender, EventArgs e)
        {
            // Po zapisie - wysyłamy powiadomienia dla dni z pending co-duty
            bool success = await SendPendingCoDutyNotificationsAsync();

            // Pokaż komunikat sukcesu lub błędu
            var dialog = App.CreateThemedDialog();
            if (success)
            {
                dialog.Title = "Zapisano";
                dialog.Content = "Deklaracje zostały zapisane pomyślnie.";
                dialog.PrimaryButtonText = "OK";
            }
            else
            {
                dialog.Title = "Błąd zapisu";
                dialog.Content = "Deklaracje zostały zapisane, ale wystąpił błąd podczas wysyłania powiadomień o współdyżurach.";
                dialog.PrimaryButtonText = "OK";
            }

            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Wysyła powiadomienia o prośbach współdyżurnych dla wszystkich dni z pending co-duty.
        /// Wywoływane TYLKO po zapisie deklaracji.
        /// </summary>
        /// <returns>True jeśli wszystkie powiadomienia zostały wysłane pomyślnie, false w przypadku błędu</returns>
        private async Task<bool> SendPendingCoDutyNotificationsAsync()
        {
            if (ViewModel == null || CoDutyNotificationRepository == null || !ActiveUnitId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[SendNotifications] Brak ViewModel, repozutorium lub ActiveUnitId");
                return true; // Brak powiadomień do wysłania - sukces
            }

            if (ViewModel.SelectedDoctor == null)
            {
                System.Diagnostics.Debug.WriteLine("[SendNotifications] Brak wybranego lekarza");
                return true; // Brak powiadomień do wysłania - sukces
            }

            try
            {
                // Pobierz listę dni z pending co-duty gdzie jestem inicjatorem
                var pendingNotifications = ViewModel.GetPendingCoDutyNotificationsToSend();

                System.Diagnostics.Debug.WriteLine($"[SendNotifications] Znaleziono {pendingNotifications.Count} powiadomień do wysłania");

                foreach (var (day, partnerId, slotPart) in pendingNotifications)
                {
                    var notification = new GrafikoMat.Core.Data.CoDutyNotification
                    {
                        FromDoctorId = ViewModel.SelectedDoctor.Id,
                        ToDoctorId = partnerId,
                        UnitId = ActiveUnitId.Value,
                        Year = ViewModel.Year,
                        Month = ViewModel.MonthIndex + 1,
                        Day = day,
                        SlotPart = Enum.Parse<SlotPart>(slotPart),
                        Status = CoDutyStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    await CoDutyNotificationRepository.CreateNotificationAsync(notification);
                    System.Diagnostics.Debug.WriteLine($"[SendNotifications] ✓ Utworzono powiadomienie dla dnia {day}, partner {partnerId}, slot {slotPart}");
                }

                if (pendingNotifications.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendNotifications] ✓ Wysłano {pendingNotifications.Count} powiadomień");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendNotifications] ✗ BŁĄD: {ex.Message}");
                return false;
            }
        }

        // Repozytoria dla współdyżurnych (ustawiane z zewnątrz)
        public GrafikoMat.Core.Repositories.ICoDutyNotificationRepository? CoDutyNotificationRepository { get; set; }
        public GrafikoMat.Core.Repositories.IDeclarationRepository? DeclarationRepository { get; set; }
        public GrafikoMat.Core.Repositories.IDoctorRepository? DoctorRepository { get; set; }

        // ID aktywnej jednostki (ustawiane z zewnątrz przez MainWindow)
        public Guid? ActiveUnitId { get; set; }

        private bool _isDragging = false;
        private int _dragStartIndex = -1;
        private SlotPart _dragStartSlotPart;
        private SelectedSlot? _lastClickedSlot;

        // Lokalne śledzenie stanu klawiszy
        private bool _ctrlDown = false;
        private bool _shiftDown = false;

        // Flaga zapobiegająca podwójnej obsłudze Ctrl+Click
        private bool _handledCtrlClickInPointerPressed = false;

        // ✅ DODANE - flaga blokująca automatyczne odświeżanie
        private bool _suppressAutomaticBrushUpdate = false;

        // P/Invoke dla pewnej detekcji klawiszy
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private static bool IsCtrlDownNative() => (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        private static bool IsShiftDownNative() => (GetKeyState(VK_SHIFT) & 0x8000) != 0;

        public DeclarationsView()
        {
            this.InitializeComponent();
            this.ActualThemeChanged += OnThemeChanged;
            this.Unloaded += OnDeclarationsViewUnloaded;

            this.Loaded += (_, __) =>
            {
                // ZMIANA: Wszystkie eventy z handledEventsToo, ale precyzyjna logika Handled
                CalendarGridView.AddHandler(PointerPressedEvent, new PointerEventHandler(Calendar_PointerPressed), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerMovedEvent, new PointerEventHandler(Calendar_PointerMoved), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerReleasedEvent, new PointerEventHandler(Calendar_PointerReleased), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerExitedEvent, new PointerEventHandler(Calendar_PointerExited), handledEventsToo: true);
                CalendarGridView.AddHandler(RightTappedEvent, new RightTappedEventHandler(Calendar_RightTapped), handledEventsToo: true);
                CalendarGridView.AddHandler(TappedEvent, new TappedEventHandler(Calendar_Tapped), handledEventsToo: true);

                // Śledzenie klawiszy
                CalendarGridView.KeyDown += CalendarGridView_KeyDown;
                CalendarGridView.KeyUp += CalendarGridView_KeyUp;
            };
        }

        public void AttachViewModel(DeclarationsViewModel vm)
        {
            // Odsubskrybuj od starego ViewModelu
            UnsubscribeFromViewModelEvents();

            this.DataContext = vm;
            if (vm != null)
            {
                vm.PropertyChanged += Vm_PropertyChanged;
                // Subskrybuj do eventów nowego ViewModelu
                SubscribeToViewModelEvents();
            }
            UpdateAllCellBrushes();
            UpdateCalendarOpacity();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ✅ DODANE - obsługa HasNoDoctors
            if (e.PropertyName == nameof(DeclarationsViewModel.HasNoDoctors))
            {
                UpdateCalendarOpacity();
            }

            if (e.PropertyName == nameof(ViewModel.SelectedDoctor))
            {
                _lastClickedSlot = null;

                // ✅ ZMIENIONE - nie odświeżaj jeśli jest flaga blokująca
                if (!_suppressAutomaticBrushUpdate)
                {
                    UpdateAllCellBrushes();
                }
            }
        }

        // ✅ DODANE - metoda do przeładowania z blokowaniem auto-update
        public void ReloadWithoutAutomaticUpdate(Action reloadAction)
        {
            _suppressAutomaticBrushUpdate = true;
            try
            {
                reloadAction();
            }
            finally
            {
                _suppressAutomaticBrushUpdate = false;
            }
        }

        // ✅ DODANE - metody animacji overlaya
        public void ShowOverlay()
        {
            System.Diagnostics.Debug.WriteLine($"[OVERLAY] ShowOverlay called");
            if (NoDoctorsOverlay != null && NoDoctorsOverlay.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine($"[OVERLAY] Starting fade-in animation");
                OverlayFadeInStoryboard?.Begin();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OVERLAY] Overlay not visible or null");
            }
        }

        public void HideOverlay()
        {
            System.Diagnostics.Debug.WriteLine($"[OVERLAY] HideOverlay called");
            if (NoDoctorsOverlay != null && NoDoctorsOverlay.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine($"[OVERLAY] Starting fade-out animation");
                OverlayFadeOutStoryboard?.Begin();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OVERLAY] Overlay not visible or null");
            }
        }

        private (int index, SlotPart part) GetIndexAndSlotFromPoint(Point p)
        {
            if (ViewModel == null || ViewModel.DayCells.Count == 0 || p.X < 0 || p.Y < 0)
                return (-1, SlotPart.Full);

            var grid = CalendarGridView;
            for (int i = 0; i < ViewModel.DayCells.Count; i++)
            {
                if (grid.ContainerFromIndex(i) is FrameworkElement container && container.ActualHeight > 0)
                {
                    var transform = container.TransformToVisual(grid);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (bounds.Contains(p))
                    {
                        var cellVM = ViewModel.DayCells[i];

                        System.Diagnostics.Debug.WriteLine($"[CLICK] Cell {i}: IsSplit={cellVM.IsSplit}, Point=({p.X},{p.Y}), Bounds=({bounds.Left},{bounds.Top},{bounds.Width},{bounds.Height})");

                        if (!cellVM.IsSplit)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CLICK] Returning Full for 24h cell");
                            return (i, SlotPart.Full);
                        }
                        else
                        {
                            var relativeY = p.Y - bounds.Top;
                            var headerHeight = 24;
                            var contentHeight = container.ActualHeight - headerHeight;

                            System.Diagnostics.Debug.WriteLine($"[CLICK] 12h cell: relativeY={relativeY}, headerHeight={headerHeight}, contentHeight={contentHeight}");

                            if (relativeY < headerHeight)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CLICK] Returning Day (header)");
                                return (i, SlotPart.Day);
                            }

                            var part = (relativeY < headerHeight + contentHeight / 2) ?
                                SlotPart.Day : SlotPart.Night;

                            System.Diagnostics.Debug.WriteLine($"[CLICK] Returning {part}");
                            return (i, part);
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CLICK] No cell found at point ({p.X},{p.Y})");
            return (-1, SlotPart.Full);
        }

        private static bool IsCtrlPressedCombined(VirtualKeyModifiers mods, bool localFlag)
        {
            bool byMods = (mods & VirtualKeyModifiers.Control) == VirtualKeyModifiers.Control;
            bool byWinUI = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            bool byNative = IsCtrlDownNative();
            return byMods || byWinUI || byNative || localFlag;
        }

        private static bool IsShiftPressedCombined(VirtualKeyModifiers mods, bool localFlag)
        {
            bool byMods = (mods & VirtualKeyModifiers.Shift) == VirtualKeyModifiers.Shift;
            bool byWinUI = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            bool byNative = IsShiftDownNative();
            return byMods || byWinUI || byNative || localFlag;
        }

        private void Calendar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var pointInfo = e.GetCurrentPoint(CalendarGridView);
            if (!pointInfo.Properties.IsLeftButtonPressed)
                return;

            CalendarGridView.Focus(FocusState.Pointer);

            var (index, slotPart) = GetIndexAndSlotFromPoint(pointInfo.Position);
            if (index == -1 || index >= ViewModel.DayCells.Count || !ViewModel.DayCells[index].InMonth)
            {
                // Kliknięcie poza komórkami - NIE blokuj eventu (pozwól ListView obsłużyć)
                return;
            }

            var mods = e.KeyModifiers;
            bool isCtrlPressed = IsCtrlPressedCombined(mods, _ctrlDown);
            bool isShiftPressed = IsShiftPressedCombined(mods, _shiftDown);

            // ✅ DODANE - szczegółowe logowanie
            System.Diagnostics.Debug.WriteLine($"[CTRL-DEBUG] mods={mods}, _ctrlDown={_ctrlDown}");
            System.Diagnostics.Debug.WriteLine($"[CTRL-DEBUG] IsCtrlPressedCombined result: {isCtrlPressed}");

            var byMods = (mods & VirtualKeyModifiers.Control) == VirtualKeyModifiers.Control;
            var byWinUI = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            var byNative = IsCtrlDownNative();
            System.Diagnostics.Debug.WriteLine($"[CTRL-DEBUG] byMods={byMods}, byWinUI={byWinUI}, byNative={byNative}");

            // CTRL - toggle bez drag
            if (isCtrlPressed)
            {
                System.Diagnostics.Debug.WriteLine($"[CTRL] Detected Ctrl+Click on index={index}, part={slotPart}");
                _isDragging = false;
                _handledCtrlClickInPointerPressed = true;
                ViewModel.ToggleSlotSelection(index, slotPart, isMultiSelect: true);
                _lastClickedSlot = new SelectedSlot(index, slotPart);
                e.Handled = true;
                return;
            }

            // SHIFT - zakres
            if (isShiftPressed && _lastClickedSlot != null)
            {
                _isDragging = false;
                ViewModel.SelectDragRange(_lastClickedSlot.Index, index, _lastClickedSlot.Part, slotPart);
                e.Handled = true;
                return;
            }

            // Brak modyfikatorów - single + drag
            _isDragging = true;
            _dragStartIndex = index;
            _dragStartSlotPart = slotPart;
            CalendarGridView.CapturePointer(e.Pointer);

            ViewModel.SelectSingleSlot(index, slotPart);
            _lastClickedSlot = new SelectedSlot(index, slotPart);
            e.Handled = true;
        }

        private void Calendar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && _isDragging)
            {
                var point = e.GetCurrentPoint(CalendarGridView).Position;
                var (currentIndex, currentPart) = GetIndexAndSlotFromPoint(point);

                if (currentIndex != -1 && currentIndex < ViewModel.DayCells.Count && ViewModel.DayCells[currentIndex].InMonth)
                {
                    ViewModel.SelectDragRange(_dragStartIndex, currentIndex, _dragStartSlotPart, currentPart);
                    e.Handled = true;
                }
            }
        }

        private void Calendar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                CalendarGridView.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void Calendar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                CalendarGridView.ReleasePointerCaptures();
                e.Handled = true;
            }
        }

        private void Calendar_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var point = e.GetPosition(CalendarGridView);
            var (index, slotPart) = GetIndexAndSlotFromPoint(point);

            if (index == -1 || index >= ViewModel.DayCells.Count || !ViewModel.DayCells[index].InMonth)
            {
                return;
            }

            var clickedSlot = new SelectedSlot(index, slotPart);

            if (!ViewModel.SelectedSlots.Contains(clickedSlot))
            {
                ViewModel.SelectSingleSlot(index, slotPart);
                _lastClickedSlot = clickedSlot;
            }

            var contextMenu = new MenuFlyout();
            contextMenu.Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedTop;

            // ✅ Styl dla przesunięcia w prawo (wyrównanie z resztą)
            var indentedStyle = new Style(typeof(MenuFlyoutItem));
            indentedStyle.Setters.Add(new Setter(MenuFlyoutItem.PaddingProperty, new Thickness(12, 12, 12, 12)));

            // ✅ Styl nagłówka z tłem
            var headerStyle = new Style(typeof(MenuFlyoutItem));
            headerStyle.Setters.Add(new Setter(MenuFlyoutItem.PaddingProperty, new Thickness(12, 12, 12, 12)));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                new WinUISolidColorBrush(WinUIColor.FromArgb(60, 128, 128, 128))));
            headerStyle.Setters.Add(new Setter(MenuFlyoutItem.FontWeightProperty, Microsoft.UI.Text.FontWeights.SemiBold));

            // ============================================
            // SEKCJA 1: Nagłówek - wyróżniony tłem
            // ============================================
            var addDeclarationHeader = new MenuFlyoutItem
            {
                Text = "Dodaj deklarację:",
                IsEnabled = false,
                Style = headerStyle
            };
            contextMenu.Items.Add(addDeclarationHeader);

            // ============================================
            // SEKCJA 2: Typy deklaracji - PRZESUNIĘTE W PRAWO + podkreślenia dla skrótów
            // ============================================
            var mogeItem = new MenuFlyoutItem
            {
                Text = "        Mogę (M)",
                Tag = "MOG",
                Style = indentedStyle
            };
            mogeItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(mogeItem);

            var chceItem = new MenuFlyoutItem
            {
                Text = "        Chcę (C)",
                Tag = "CHC",
                Style = indentedStyle
            };
            chceItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(chceItem);

            var warunkowoItem = new MenuFlyoutItem
            {
                Text = "        Mogę warunkowo (W)",
                Tag = "WAR",
                Style = indentedStyle
            };
            warunkowoItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(warunkowoItem);

            var rezerwacjaItem = new MenuFlyoutItem
            {
                Text = "        Rezerwacja (R)",
                Tag = "REZ",
                Style = indentedStyle
            };
            rezerwacjaItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(rezerwacjaItem);

            var nieMogeItem = new MenuFlyoutItem
            {
                Text = "        Nie mogę (N)",
                Tag = "---",
                Style = indentedStyle
            };
            nieMogeItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(nieMogeItem);

            var innyDyzurItem = new MenuFlyoutItem
            {
                Text = "        Inny dyżur (D)",
                Tag = "DYZ",
                Style = indentedStyle
            };
            innyDyzurItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(innyDyzurItem);

            var urlopItem = new MenuFlyoutItem
            {
                Text = "        Urlop (U)",
                Tag = "URL",
                Style = indentedStyle
            };
            urlopItem.Click += OnDeclarationMenuItemClick;
            contextMenu.Items.Add(urlopItem);

            contextMenu.Items.Add(new MenuFlyoutSeparator());

            // ============================================
            // SEKCJA 3: Współdyżurny z rozwijaną listą
            // ============================================

            // ✅ SPRAWDŹ czy w zaznaczonych slotach są deklaracje
            bool hasDeclarationInSelection = ViewModel.SelectedSlots
                .Any(slot => ViewModel.HasDeclarationInSlot(slot.Index, slot.Part));

            // ✅ SPRAWDŹ czy w zaznaczonych slotach są deklaracje negatywne (nie mogę, urlop, inny dyżur)
            bool hasNegativeDeclarationInSelection = ViewModel.SelectedSlots
                .Any(slot =>
                {
                    var cell = ViewModel.DayCells[slot.Index];
                    string symbol = slot.Part switch
                    {
                        SlotPart.Full => cell.SymbolFull,
                        SlotPart.Day => cell.SymbolDay,
                        SlotPart.Night => cell.SymbolNight,
                        _ => ""
                    };
                    return symbol == "---" || symbol == "URL" || symbol == "DYZ";
                });

            var addCoWorkerSubItem = new MenuFlyoutSubItem
            {
                Text = "Dodaj współdyżurnego"
                // ✅ SubItem ZAWSZE aktywne (można rozwinąć)
            };

            if (!hasDeclarationInSelection)
            {
                // ✅ Pokaż komunikat zamiast listy lekarzy
                var hintItem = new MenuFlyoutItem
                {
                    Text = "Najpierw wstaw deklarację",
                    IsEnabled = false
                };
                addCoWorkerSubItem.Items.Add(hintItem);
            }
            else if (hasNegativeDeclarationInSelection)
            {
                // ✅ Deklaracje negatywne - nie można dodać współdyżurnego
                var hintItem = new MenuFlyoutItem
                {
                    Text = "Nie można dodać do tej deklaracji",
                    IsEnabled = false
                };
                addCoWorkerSubItem.Items.Add(hintItem);
            }
            else
            {
                // ✅ Pokaż listę lekarzy tylko gdy są deklaracje
                var currentDoctorId = ViewModel.SelectedDoctor?.Id;
                var doctorsInUnit = ViewModel.Doctors
                    .Where(d => d.Profile.Id != currentDoctorId)
                    .ToList();

                if (doctorsInUnit.Any())
                {
                    foreach (var doctor in doctorsInUnit)
                    {
                        var doctorItem = new MenuFlyoutItem
                        {
                            Text = doctor.DisplayName,
                            Tag = doctor.Profile.Id
                        };
                        doctorItem.Click += OnAddCoDutyPartnerClick;
                        addCoWorkerSubItem.Items.Add(doctorItem);
                    }
                }
                else
                {
                    var nodoctorsItem = new MenuFlyoutItem
                    {
                        Text = "(brak innych dyżurnych)",
                        IsEnabled = false
                    };
                    addCoWorkerSubItem.Items.Add(nodoctorsItem);
                }
            }

            contextMenu.Items.Add(addCoWorkerSubItem);

            contextMenu.Items.Add(new MenuFlyoutSeparator());

            // ============================================
            // SEKCJA 4: Wyczyść deklarację i Usuń współdyżurnego
            // ============================================

            // Sprawdź czy którykolwiek zaznaczony slot ma współdyżurnego
            bool hasCoDutyInSelection = ViewModel.SelectedSlots
                .Any(slot =>
                {
                    if (slot.Index < 0 || slot.Index >= ViewModel.DayCells.Count)
                        return false;
                    var cell = ViewModel.DayCells[slot.Index];
                    return slot.Part switch
                    {
                        SlotPart.Full => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerFull),
                        SlotPart.Day => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerDay),
                        SlotPart.Night => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerNight),
                        _ => false
                    };
                });

            // "Usuń współdyżurnego" - widoczne TYLKO gdy jest współdyżurny
            if (hasCoDutyInSelection)
            {
                var removeCoDutyItem = new MenuFlyoutItem
                {
                    Text = "Usuń współdyżurnego"
                };
                removeCoDutyItem.Click += OnRemoveCoDutyPartnerClick;
                contextMenu.Items.Add(removeCoDutyItem);
            }

            // "Wyczyść deklarację" - widoczne gdy jest JAKAKOLWIEK zawartość
            if (hasDeclarationInSelection || hasCoDutyInSelection)
            {
                var clearDeclarationItem = new MenuFlyoutItem
                {
                    Text = "Wyczyść deklarację"
                };
                clearDeclarationItem.Click += OnClearDeclarationClick;
                contextMenu.Items.Add(clearDeclarationItem);
            }

            if (hasDeclarationInSelection || hasCoDutyInSelection)
            {
                contextMenu.Items.Add(new MenuFlyoutSeparator());
            }

            // ============================================
            // SEKCJA 5: Przełącznik trybu
            // ============================================
            var toggleModeItem = new MenuFlyoutItem
            {
                Text = "Przełącz tryb (12h/24h)"
            };
            toggleModeItem.Click += OnToggleModeMenuItemClick;
            contextMenu.Items.Add(toggleModeItem);

            contextMenu.ShowAt(CalendarGridView, point);

            e.Handled = true;
        }

        private void Calendar_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Jeśli PointerPressed obsłużył Ctrl, ignore
            if (_handledCtrlClickInPointerPressed)
            {
                _handledCtrlClickInPointerPressed = false;
                e.Handled = true;
                return;
            }

            // ✅ ZMIENIONE - sprawdź CTRL również w Tapped
            bool isCtrlPressed = _ctrlDown ||
                                (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down ||
                                IsCtrlDownNative();

            if (!isCtrlPressed)
            {
                // Nie-Ctrl kliknięcie - już obsłużone przez PointerPressed
                return;
            }

            // Fallback: Ctrl+Click który nie został złapany przez PointerPressed
            if (ViewModel == null) return;

            var point = e.GetPosition(CalendarGridView);
            var (index, slotPart) = GetIndexAndSlotFromPoint(point);

            if (index >= 0 && index < ViewModel.DayCells.Count && ViewModel.DayCells[index].InMonth)
            {
                System.Diagnostics.Debug.WriteLine($"[TAPPED] Handling Ctrl+Click in Tapped fallback");
                ViewModel.ToggleSlotSelection(index, slotPart, isMultiSelect: true);
                _lastClickedSlot = new SelectedSlot(index, slotPart);
            }

            e.Handled = true;
        }

        private async void CalendarGridView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _ctrlDown = true;
            if (e.Key == VirtualKey.Shift) _shiftDown = true;

            // DEL - wyczyść wszystko z dialogiem
            if (e.Key == VirtualKey.Delete && ViewModel != null && ViewModel.SelectedSlots.Any())
            {
                await ClearAllFromSelectedSlotsAsync();
                e.Handled = true;
                return;
            }

            // Skróty klawiaturowe dla deklaracji
            if (ViewModel != null && ViewModel.SelectedSlots.Any())
            {
                string? declarationCode = e.Key switch
                {
                    VirtualKey.M => "MOG",     // m - mogę
                    VirtualKey.C => "CHC",     // c - chcę
                    VirtualKey.W => "WAR",     // w - mogę warunkowo
                    VirtualKey.R => "REZ",     // r - rezerwacja
                    VirtualKey.D => "DYZ",     // d - inny dyżur
                    VirtualKey.U => "URL",     // u - urlop
                    VirtualKey.N => "---",     // n - nie mogę
                    (VirtualKey)189 => "---", // - (klawisz minus, VK_OEM_MINUS)
                    _ => null
                };

                if (declarationCode != null)
                {
                    await ApplyDeclarationToSelectedSlotsAsync(declarationCode);
                    e.Handled = true;
                }
            }
        }

        private void CalendarGridView_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _ctrlDown = false;
            if (e.Key == VirtualKey.Shift) _shiftDown = false;
        }

        /// <summary>
        /// DEL - wyczyść wszystko (symbol + współdyżurny) z dialogiem potwierdzenia
        /// </summary>
        private async Task ClearAllFromSelectedSlotsAsync()
        {
            if (ViewModel == null || ViewModel.SelectedDoctor == null)
                return;

            var selectedSlots = ViewModel.SelectedSlots.ToList();
            if (!selectedSlots.Any())
                return;

            // Dialog potwierdzenia
            var confirmDialog = App.CreateThemedDialog();
            confirmDialog.Title = "Wyczyść sloty";
            confirmDialog.Content = selectedSlots.Count == 1
                ? "Czy na pewno wyczyścić ten slot?"
                : $"Czy na pewno wyczyścić {selectedSlots.Count} slotów?";
            confirmDialog.PrimaryButtonText = "Wyczyść";
            confirmDialog.CloseButtonText = "Anuluj";
            confirmDialog.DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close;
            confirmDialog.XamlRoot = this.XamlRoot;

            var result = await confirmDialog.ShowAsync();
            if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                return;

            // Wyczyść wszystko
            foreach (var slot in selectedSlots)
            {
                var cell = ViewModel.DayCells.FirstOrDefault(c => c.Index == slot.Index);
                if (cell == null || !cell.InMonth)
                    continue;

                switch (slot.Part)
                {
                    case SlotPart.Full:
                        cell.SymbolFull = "";
                        cell.CoDutyPartnerFull = "";
                        cell.CoDutyStatusGlyphFull = "";
                        break;
                    case SlotPart.Day:
                        cell.SymbolDay = "";
                        cell.CoDutyPartnerDay = "";
                        cell.CoDutyStatusGlyphDay = "";
                        break;
                    case SlotPart.Night:
                        cell.SymbolNight = "";
                        cell.CoDutyPartnerNight = "";
                        cell.CoDutyStatusGlyphNight = "";
                        break;
                }

                // Wyczyść w _sharedDeclarations
                ViewModel.UpdateCoDutyFields(ViewModel.SelectedDoctor.Id, cell.Date.Day, null, null, null, null);
            }
        }

        /// <summary>
        /// Aplikuje deklarację na zaznaczone sloty.
        /// Dla MOG/CHC/WAR/REZ zachowuje współdyżurnego.
        /// Dla ---/DYZ/URL pokazuje dialog i usuwa współdyżurnego po akceptacji.
        /// </summary>
        private async Task ApplyDeclarationToSelectedSlotsAsync(string declarationCode)
        {
            if (ViewModel == null || ViewModel.SelectedDoctor == null)
                return;

            var selectedSlots = ViewModel.SelectedSlots.ToList();

            // Sprawdź czy którykolwiek slot ma współdyżurnego
            bool hasCoDutyInAnySlot = selectedSlots.Any(slot =>
            {
                if (slot.Index < 0 || slot.Index >= ViewModel.DayCells.Count)
                    return false;
                var cell = ViewModel.DayCells[slot.Index];
                return slot.Part switch
                {
                    SlotPart.Full => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerFull),
                    SlotPart.Day => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerDay),
                    SlotPart.Night => !string.IsNullOrWhiteSpace(cell.CoDutyPartnerNight),
                    _ => false
                };
            });

            // Symbole które wymagają usunięcia współdyżurnego
            bool requiresCoDutyRemoval = declarationCode is "---" or "DYZ" or "URL";

            // Jeśli jest współdyżurny i nowy symbol wymaga usunięcia - pokaż dialog
            if (hasCoDutyInAnySlot && requiresCoDutyRemoval)
            {
                var confirmDialog = App.CreateThemedDialog();
                confirmDialog.Title = "Zmiana deklaracji";
                confirmDialog.Content = "Zmiana deklaracji na \"" + declarationCode + "\" usunie współdyżurnego. Kontynuować?";
                confirmDialog.PrimaryButtonText = "Tak, zmień";
                confirmDialog.CloseButtonText = "Anuluj";
                confirmDialog.DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close;
                confirmDialog.XamlRoot = this.XamlRoot;

                var result = await confirmDialog.ShowAsync();
                if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                    return; // User anulował
            }

            // Aplikuj deklarację
            foreach (var selectedSlot in selectedSlots)
            {
                if (selectedSlot.Index < 0 || selectedSlot.Index >= ViewModel.DayCells.Count)
                    continue;

                var cell = ViewModel.DayCells[selectedSlot.Index];
                if (!cell.InMonth)
                    continue;

                // Ustaw symbol
                switch (selectedSlot.Part)
                {
                    case SlotPart.Full:
                        cell.SymbolFull = declarationCode;
                        if (requiresCoDutyRemoval)
                        {
                            cell.CoDutyPartnerFull = "";
                            cell.CoDutyStatusGlyphFull = "";
                        }
                        break;

                    case SlotPart.Day:
                        cell.SymbolDay = declarationCode;
                        if (requiresCoDutyRemoval)
                        {
                            cell.CoDutyPartnerDay = "";
                            cell.CoDutyStatusGlyphDay = "";
                        }
                        break;

                    case SlotPart.Night:
                        cell.SymbolNight = declarationCode;
                        if (requiresCoDutyRemoval)
                        {
                            cell.CoDutyPartnerNight = "";
                            cell.CoDutyStatusGlyphNight = "";
                        }
                        break;
                }

                // Jeśli wymagane usunięcie współdyżurnego - wyczyść w _sharedDeclarations
                if (requiresCoDutyRemoval)
                {
                    ViewModel.UpdateCoDutyFields(ViewModel.SelectedDoctor.Id, cell.Date.Day, null, null, null, null);
                }
            }
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            UpdateAllCellBrushes();
            UpdateCalendarOpacity();
        }

        // ✅ DODANA NOWA METODA - kontrola opacity kalendarza
        public void UpdateCalendarOpacity()
        {
            System.Diagnostics.Debug.WriteLine($"[OPACITY] UpdateCalendarOpacity called");

            if (ViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine($"[OPACITY] ViewModel is null");
                return;
            }

            bool isDark = this.ActualTheme == ElementTheme.Dark ||
                          (this.ActualTheme == ElementTheme.Default &&
                           Application.Current.RequestedTheme == ApplicationTheme.Dark);

            System.Diagnostics.Debug.WriteLine($"[OPACITY] isDark={isDark}, HasNoDoctors={ViewModel.HasNoDoctors}");

            var calendarWrapper = CalendarWrapper;

            if (calendarWrapper == null)
            {
                System.Diagnostics.Debug.WriteLine($"[OPACITY] CalendarWrapper is NULL!");
                return;
            }

            // W ciemnym: bardziej przezroczysty (mniejsza opacity)
            // W jasnym: obecna wartość OK
            double targetOpacity = ViewModel.HasNoDoctors
                ? (isDark ? 0.10 : 0.15)
                : 1.0;

            System.Diagnostics.Debug.WriteLine($"[OPACITY] Setting opacity to {targetOpacity}");
            calendarWrapper.Opacity = targetOpacity;
            System.Diagnostics.Debug.WriteLine($"[OPACITY] CalendarWrapper.Opacity after set = {calendarWrapper.Opacity}");
        }

        public void UpdateAllCellBrushes()
        {
            if (ViewModel?.DayCells == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateAllCellBrushes: ViewModel lub DayCells są null!");
                return;
            }

            bool isDark = this.ActualTheme == ElementTheme.Dark ||
                          (this.ActualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

            foreach (var cell in ViewModel.DayCells)
            {
                string effectiveBackground;
                string effectiveBorder;
                string dayNumberFg;
                string headerBg;

                if (cell.IsFullSelected || cell.IsDaySelected || cell.IsNightSelected)
                {
                    effectiveBackground = "#FF4287F5"; // WinUIColor.FromArgb(255, 66, 135, 245)
                    effectiveBorder = "#FF4287F5";
                    dayNumberFg = "#FFFFFFFF"; // White
                    headerBg = "#FF326EC8"; // WinUIColor.FromArgb(255, 50, 110, 200)
                }
                else if (!cell.InMonth)
                {
                    if (isDark)
                    {
                        effectiveBackground = cell.IsDayOff ? "#283C3C3C" : "#28323232"; // FromArgb(40, 60/50, 60/50, 60/50)
                        effectiveBorder = "#0AFFFFFF"; // FromArgb(10, 255, 255, 255)
                        dayNumberFg = "#FFFFFFFF"; // White
                        headerBg = "#28464646"; // FromArgb(40, 70, 70, 70)
                    }
                    else
                    {
                        effectiveBackground = cell.IsDayOff ? "#28DCDCDC" : "#28F0F0F0"; // FromArgb(40, 220/240, ...)
                        effectiveBorder = "#0A000000"; // FromArgb(10, 0, 0, 0)
                        dayNumberFg = "#FF000000"; // Black
                        headerBg = "#28E6E6E6"; // FromArgb(40, 230, 230, 230)
                    }
                }
                else
                {
                    if (isDark)
                    {
                        effectiveBackground = cell.IsDayOff ? "#96323232" : "#96282828"; // FromArgb(150, 50/40, ...)
                        effectiveBorder = "#50FFFFFF"; // FromArgb(80, 255, 255, 255)
                        dayNumberFg = "#FFFFFFFF"; // White
                        headerBg = "#963C3C3C"; // FromArgb(150, 60, 60, 60)
                    }
                    else
                    {
                        effectiveBackground = cell.IsDayOff ? "#96EBEBEB" : "#96FAFAFA"; // FromArgb(150, 235/250, ...)
                        effectiveBorder = "#50000000"; // FromArgb(80, 0, 0, 0)
                        dayNumberFg = "#FF000000"; // Black
                        headerBg = "#96F5F5F5"; // FromArgb(150, 245, 245, 245)
                    }
                }

                cell.EffectiveBackground = effectiveBackground;
                cell.EffectiveBorderBrush = effectiveBorder;
                cell.DayNumberForeground = dayNumberFg;
                cell.EffectiveHeaderBackground = headerBg;
            }
        }

        /// <summary>
        /// Obsługa kliknięcia w element menu deklaracji (MOG, CHC, WAR, itp.)
        /// </summary>
        private async void OnDeclarationMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string declarationCode)
                return;

            System.Diagnostics.Debug.WriteLine($"[MENU] Declaration clicked: {declarationCode}");
            await ApplyDeclarationToSelectedSlotsAsync(declarationCode);
        }

        /// <summary>
        /// Obsługa przełączania trybu 24h/12h dla zaznaczonych dni
        /// </summary>
        private async void OnToggleModeMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[MENU] Toggle mode clicked");

            // Zbierz unikalne indeksy dni (bez względu na część)
            var uniqueDayIndices = ViewModel.SelectedSlots
                .Select(s => s.Index)
                .Distinct()
                .ToList();

            // Sprawdź czy którykolwiek z zaznaczonych dni ma zawartość (deklarację)
            bool hasAnyContent = false;
            foreach (var dayIndex in uniqueDayIndices)
            {
                if (dayIndex < 0 || dayIndex >= ViewModel.DayCells.Count)
                    continue;

                var cell = ViewModel.DayCells[dayIndex];
                if (!cell.InMonth)
                    continue;

                if (!string.IsNullOrWhiteSpace(cell.SymbolFull) ||
                    !string.IsNullOrWhiteSpace(cell.SymbolDay) ||
                    !string.IsNullOrWhiteSpace(cell.SymbolNight))
                {
                    hasAnyContent = true;
                    break;
                }
            }

            // Jeśli jest zawartość - pokaż ostrzeżenie
            if (hasAnyContent)
            {
                var confirmDialog = App.CreateThemedDialog();
                confirmDialog.Title = "Zmiana trybu dnia";
                confirmDialog.Content = "Zmiana trybu dnia (24h ↔ 12h) może spowodować utratę lub zmianę deklaracji.\n\n" +
                                       "Po zapisaniu zmiany zostaną nadpisane w bazie danych.\n\n" +
                                       "Czy kontynuować?";
                confirmDialog.PrimaryButtonText = "Kontynuuj";
                confirmDialog.CloseButtonText = "Anuluj";
                confirmDialog.DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close;
                confirmDialog.XamlRoot = this.XamlRoot;

                var result = await confirmDialog.ShowAsync();
                if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    System.Diagnostics.Debug.WriteLine($"[MENU] Toggle mode cancelled by user");
                    return; // Użytkownik anulował
                }
            }

            // Zapamiętaj zaznaczenie przed zmianą trybu
            var selectedIndices = new HashSet<int>(uniqueDayIndices);

            foreach (var dayIndex in uniqueDayIndices)
            {
                if (dayIndex < 0 || dayIndex >= ViewModel.DayCells.Count)
                    continue;

                var cell = ViewModel.DayCells[dayIndex];
                if (!cell.InMonth)
                    continue;

                // Przełącz tryb
                if (cell.IsSplit)
                {
                    // Z 12h na 24h - skopiuj dane z Day do Full
                    cell.IsSplit = false;
                    if (!string.IsNullOrWhiteSpace(cell.SymbolDay))
                        cell.SymbolFull = cell.SymbolDay;
                    else if (!string.IsNullOrWhiteSpace(cell.SymbolNight))
                        cell.SymbolFull = cell.SymbolNight;

                    cell.SymbolDay = "";
                    cell.SymbolNight = "";
                    System.Diagnostics.Debug.WriteLine($"[MENU] Switched day {cell.Date.Day} from 12h to 24h");
                }
                else
                {
                    // Z 24h na 12h - skopiuj dane z Full do Day
                    cell.IsSplit = true;
                    if (!string.IsNullOrWhiteSpace(cell.SymbolFull))
                    {
                        cell.SymbolDay = cell.SymbolFull;
                        cell.SymbolNight = cell.SymbolFull;
                    }
                    cell.SymbolFull = "";
                    System.Diagnostics.Debug.WriteLine($"[MENU] Switched day {cell.Date.Day} from 24h to 12h");
                }
            }

            // Przywróć zaznaczenie - zaznacz pełne sloty dla zmienionych dni
            ViewModel.ClearSelection();
            foreach (var dayIndex in selectedIndices)
            {
                if (dayIndex < 0 || dayIndex >= ViewModel.DayCells.Count)
                    continue;

                var cell = ViewModel.DayCells[dayIndex];
                if (!cell.InMonth)
                    continue;

                // Zaznacz odpowiedni slot w zależności od nowego trybu
                if (cell.IsSplit)
                {
                    // Tryb 12h - zaznacz oba sloty (dzień i noc)
                    ViewModel.SelectedSlots.Add(new SelectedSlot(dayIndex, SlotPart.Day));
                    ViewModel.SelectedSlots.Add(new SelectedSlot(dayIndex, SlotPart.Night));
                }
                else
                {
                    // Tryb 24h - zaznacz pełny slot
                    ViewModel.SelectedSlots.Add(new SelectedSlot(dayIndex, SlotPart.Full));
                }
            }

            // Odśwież wizualizację zaznaczenia
            ViewModel.UpdateSelectionVisuals();
        }

        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= Vm_PropertyChanged;
            }

            // Odsubskrybuj od eventów ViewModelu
            UnsubscribeFromViewModelEvents();

            this.Unloaded -= OnDeclarationsViewUnloaded;
            this.ActualThemeChanged -= OnThemeChanged;

            if (CalendarGridView != null)
            {
                CalendarGridView.RemoveHandler(PointerPressedEvent, new PointerEventHandler(Calendar_PointerPressed));
                CalendarGridView.RemoveHandler(PointerMovedEvent, new PointerEventHandler(Calendar_PointerMoved));
                CalendarGridView.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(Calendar_PointerReleased));
                CalendarGridView.RemoveHandler(PointerExitedEvent, new PointerEventHandler(Calendar_PointerExited));
                CalendarGridView.RemoveHandler(RightTappedEvent, new RightTappedEventHandler(Calendar_RightTapped));
                CalendarGridView.RemoveHandler(TappedEvent, new TappedEventHandler(Calendar_Tapped));

                CalendarGridView.KeyDown -= CalendarGridView_KeyDown;
                CalendarGridView.KeyUp -= CalendarGridView_KeyUp;
            }
        }

        // ============================================================================
        // Obsługa współdyżurnych
        // ============================================================================

        private async void OnAddCoDutyPartnerClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not Guid partnerId)
                return;

            if (ViewModel == null || CoDutyNotificationRepository == null || DoctorRepository == null)
            {
                System.Diagnostics.Debug.WriteLine("[DeclarationsView] Brak wymaganych zależności dla współdyżurnych");
                return;
            }

            var currentDoctor = ViewModel.SelectedDoctor;
            if (currentDoctor == null)
                return;

            var selectedSlots = ViewModel.SelectedSlots.ToList();
            if (!selectedSlots.Any())
                return;

            try
            {
                // Pobierz dane partnera z ViewModel.Doctors (mają już uwzględnione duplikaty w DisplayName)
                var partner = ViewModel.Doctors.FirstOrDefault(d => d.Profile.Id == partnerId);
                if (partner == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeclarationsView] Nie znaleziono partnera: {partnerId}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DeclarationsView] DODAWANIE WSPÓŁDYŻURNEGO:");
                System.Diagnostics.Debug.WriteLine($"  currentDoctor.Id = {currentDoctor.Id} ({currentDoctor.FullName})");
                System.Diagnostics.Debug.WriteLine($"  partnerId = {partnerId} ({partner.Profile.FullName})");

                // Pobierz ID jednostki z ActiveUnitId (ustawione przez MainWindow)
                if (!ActiveUnitId.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeclarationsView] BŁĄD: Brak ActiveUnitId - nie można utworzyć powiadomienia");
                    var errorDialog = App.CreateThemedDialog();
                    errorDialog.Title = "Błąd";
                    errorDialog.Content = "Nie można wysłać prośby - brak informacji o jednostce.";
                    errorDialog.CloseButtonText = "OK";
                    errorDialog.XamlRoot = this.XamlRoot;
                    await errorDialog.ShowAsync();
                    return;
                }

                var unitId = ActiveUnitId.Value;

                // Dla każdego zaznaczonego slotu
                foreach (var slot in selectedSlots)
                {
                    var dayCell = ViewModel.DayCells.FirstOrDefault(c => c.Index == slot.Index);
                    if (dayCell == null || !dayCell.InMonth)
                        continue;

                    string slotPart = slot.Part switch
                    {
                        SlotPart.Full => "full",
                        SlotPart.Day => "day",
                        SlotPart.Night => "night",
                        _ => "full"
                    };

                    // Aktualizuj TYLKO LOKALNIE deklaracje obu lekarzy (w pamięci)
                    // Powiadomienie zostanie wysłane dopiero po kliknięciu "Zapisz"
                    UpdateLocalCoDutyDeclarations(
                        currentDoctor.Id,
                        partnerId,
                        dayCell.Date.Day,
                        slotPart,
                        currentDoctor.Id); // Inicjator = currentDoctor

                    System.Diagnostics.Debug.WriteLine($"[DeclarationsView] Zaznaczono lokalnie współdyżur: {currentDoctor.FullName} + {partner.Profile.FullName} na dzień {dayCell.Date.Day} ({slotPart})");
                }

                // Odśwież TYLKO pola współdyżurnych w UI (bez przeładowywania całego widoku)
                RefreshCoDutyUIFromSharedDeclarations(currentDoctor.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeclarationsView] Błąd dodawania współdyżurnego: {ex.Message}");

                var errorDialog = App.CreateThemedDialog();
                errorDialog.Title = "Błąd";
                errorDialog.Content = $"Nie udało się wysłać prośby: {ex.Message}";
                errorDialog.CloseButtonText = "OK";
                errorDialog.XamlRoot = this.XamlRoot;
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>
        /// Aktualizuje LOKALNIE (w ViewModel) deklaracje obu lekarzy - ustawia partnera, status i inicjatora.
        /// Nie zapisuje do bazy - to nastąpi po kliknięciu "Zapisz".
        /// </summary>
        private void UpdateLocalCoDutyDeclarations(
            Guid fromDoctorId,
            Guid toDoctorId,
            int day,
            string slotPart,
            Guid initiatorId)
        {
            if (ViewModel == null)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal] Aktualizuję lokalnie deklaracje dla dnia {day} ({slotPart})");
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   fromDoctorId = {fromDoctorId}");
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   toDoctorId = {toDoctorId}");
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   initiatorId = {initiatorId}");

                // Znajdź lekarzy w ViewModel
                var fromDoctor = ViewModel.Doctors.FirstOrDefault(d => d.Profile.Id == fromDoctorId);
                var toDoctor = ViewModel.Doctors.FirstOrDefault(d => d.Profile.Id == toDoctorId);

                if (fromDoctor == null || toDoctor == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateLocal] Nie znaleziono lekarzy w ViewModel");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   fromDoctor.Profile.Id = {fromDoctor.Profile.Id} ({fromDoctor.Profile.FullName})");
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   toDoctor.Profile.Id = {toDoctor.Profile.Id} ({toDoctor.Profile.FullName})");

                // Aktualizuj lokalną deklarację inicjatora (fromDoctor dostaje toDoctorId jako partnera)
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal] >> Aktualizuję deklarację fromDoctor.Id={fromDoctor.Profile.Id}: partnerId = {toDoctorId}");
                UpdateDoctorLocalDeclaration(fromDoctor.Profile.Id, day, slotPart, toDoctorId, initiatorId);

                // Aktualizuj lokalną deklarację partnera (toDoctor dostaje fromDoctorId jako partnera)
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal] >> Aktualizuję deklarację toDoctor.Id={toDoctor.Profile.Id}: partnerId = {fromDoctorId}");
                UpdateDoctorLocalDeclaration(toDoctor.Profile.Id, day, slotPart, fromDoctorId, initiatorId);

                System.Diagnostics.Debug.WriteLine($"[UpdateLocal] ✓ Zaktualizowano lokalnie deklaracje");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLocal] ✗ BŁĄD: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje lokalną deklarację pojedynczego lekarza w _sharedDeclarations.
        /// </summary>
        private void UpdateDoctorLocalDeclaration(
            Guid doctorId,
            int day,
            string slotPart,
            Guid partnerId,
            Guid initiatorId)
        {
            if (ViewModel == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[UpdateLocal] Aktualizacja dla doctorId={doctorId}, dzień {day}, slotPart={slotPart}");
            System.Diagnostics.Debug.WriteLine($"[UpdateLocal]   partnerId = {partnerId}, initiatorId = {initiatorId}");

            // Aktualizuj pola co-duty w lokalnej deklaracji
            ViewModel.UpdateCoDutyFields(doctorId, day, partnerId, "pending", initiatorId, slotPart);
        }

        /// <summary>
        /// Odświeża UI współdyżurnych dla zaznaczonych slotów (bez czyszczenia symboli dyżurów).
        /// </summary>
        private void RefreshCoDutyUI(List<SelectedSlot> selectedSlots, string partnerAbbreviation)
        {
            if (ViewModel == null)
                return;

            foreach (var slot in selectedSlots)
            {
                var dayCell = ViewModel.DayCells.FirstOrDefault(c => c.Index == slot.Index);
                if (dayCell == null || !dayCell.InMonth)
                    continue;

                // Ustaw informacje o współdyżurnym w UI
                string statusGlyph = "⏳"; // pending

                if (dayCell.IsSplit)
                {
                    // Tryb Split12 - aktualizuj tylko odpowiedni slot
                    if (slot.Part == SlotPart.Day)
                    {
                        dayCell.CoDutyPartnerDay = partnerAbbreviation;
                        dayCell.CoDutyStatusGlyphDay = statusGlyph;
                        System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyUI] Ustawiono Day slot: {partnerAbbreviation}");
                    }
                    else if (slot.Part == SlotPart.Night)
                    {
                        dayCell.CoDutyPartnerNight = partnerAbbreviation;
                        dayCell.CoDutyStatusGlyphNight = statusGlyph;
                        System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyUI] Ustawiono Night slot: {partnerAbbreviation}");
                    }
                }
                else
                {
                    // Tryb Full24 - aktualizuj Full slot
                    dayCell.CoDutyPartnerFull = partnerAbbreviation;
                    dayCell.CoDutyStatusGlyphFull = statusGlyph;
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyUI] Ustawiono Full slot: {partnerAbbreviation}");
                }

                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyUI] Odświeżono UI dla dnia {dayCell.Date.Day}, slot={slot.Part}, IsSplit={dayCell.IsSplit}");
            }
        }

        /// <summary>
        /// Odświeża pola współdyżurnych w UI na podstawie danych z _sharedDeclarations.
        /// Nie dotyka symboli dyżurów (SymbolFull, SymbolDay, SymbolNight) - aktualizuje TYLKO informacje o partnerach.
        /// </summary>
        private void RefreshCoDutyUIFromSharedDeclarations(Guid doctorId)
        {
            if (ViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] ViewModel is null");
                return;
            }

            var doctor = ViewModel.Doctors.FirstOrDefault(d => d.Profile.Id == doctorId);
            if (doctor == null)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Doctor not found: {doctorId}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Refreshing co-duty UI for {doctor.Profile.FullName} (ID={doctor.Profile.Id})");

            // Pobierz deklarację z shared state
            var key = $"{doctor.Profile.Id}|{ViewModel.Year:D4}-{ViewModel.MonthIndex:D2}";

            // Użyj reflection aby dostać się do _sharedDeclarations (prywatne pole w ViewModel)
            var sharedDeclarationsField = ViewModel.GetType().GetField("_sharedDeclarations",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (sharedDeclarationsField == null)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Cannot access _sharedDeclarations field");
                return;
            }

            var sharedDeclarations = sharedDeclarationsField.GetValue(ViewModel) as Dictionary<string, GrafikoMat.Models.DoctorMonthDeclaration>;
            if (sharedDeclarations == null || !sharedDeclarations.TryGetValue(key, out var declaration))
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] No declaration found for key: {key}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Found declaration with {declaration.Days.Length} days");

            // Najpierw wyczyść WSZYSTKIE pola co-duty we wszystkich komórkach
            foreach (var cell in ViewModel.DayCells.Where(c => c.InMonth))
            {
                cell.CoDutyPartnerFull = "";
                cell.CoDutyStatusGlyphFull = "";
                cell.CoDutyPartnerDay = "";
                cell.CoDutyStatusGlyphDay = "";
                cell.CoDutyPartnerNight = "";
                cell.CoDutyStatusGlyphNight = "";
            }

            // Dla każdego dnia który ma współdyżurnego
            for (int dayIndex = 0; dayIndex < declaration.Days.Length; dayIndex++)
            {
                var dayDeclaration = declaration.Days[dayIndex];

                if (!dayDeclaration.CoDutyPartnerId.HasValue)
                    continue;

                // Znajdź odpowiadającą komórkę UI
                int dayNumber = dayIndex + 1;
                var dayCell = ViewModel.DayCells.FirstOrDefault(c => c.InMonth && c.Date.Day == dayNumber);

                if (dayCell == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Day cell not found for day {dayNumber}");
                    continue;
                }

                // Pobierz dane partnera
                var partner = ViewModel.Doctors.FirstOrDefault(d => d.Profile.Id == dayDeclaration.CoDutyPartnerId.Value);
                if (partner == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Partner not found: {dayDeclaration.CoDutyPartnerId.Value}");
                    continue;
                }

                // Użyj DisplayName który uwzględnia duplikaty (skrót tylko przy duplikatach)
                string partnerDisplayName = $"z {partner.DisplayName}";
                string statusGlyph = dayDeclaration.CoDutyStatus.HasValue && (int)dayDeclaration.CoDutyStatus.Value == (int)GrafikoMat.Models.CoDutyStatus.Accepted ? "👥" : "⏳";

                // Określ który slot aktualizować na podstawie CoDutySlotPart
                string slotPart = dayDeclaration.CoDutySlotPart.HasValue
                    ? dayDeclaration.CoDutySlotPart.Value.ToString().ToLowerInvariant()
                    : "full";

                System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Day {dayNumber}:");
                System.Diagnostics.Debug.WriteLine($"  partnerId z deklaracji = {dayDeclaration.CoDutyPartnerId}");
                System.Diagnostics.Debug.WriteLine($"  partnerDisplayName = {partnerDisplayName}");
                System.Diagnostics.Debug.WriteLine($"  status = {dayDeclaration.CoDutyStatus}, glyph = {statusGlyph}");
                System.Diagnostics.Debug.WriteLine($"  slotPart = {slotPart}");

                // Aktualizuj TYLKO pola co-duty, nie dotykaj symboli
                if (slotPart == "full")
                {
                    dayCell.CoDutyPartnerFull = partnerDisplayName;
                    dayCell.CoDutyStatusGlyphFull = statusGlyph;
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Updated Full slot for day {dayNumber}: '{partnerDisplayName}' {statusGlyph}");
                }
                else if (slotPart == "day")
                {
                    dayCell.CoDutyPartnerDay = partnerDisplayName;
                    dayCell.CoDutyStatusGlyphDay = statusGlyph;
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Updated Day slot for day {dayNumber}: '{partnerDisplayName}' {statusGlyph}");
                }
                else if (slotPart == "night")
                {
                    dayCell.CoDutyPartnerNight = partnerDisplayName;
                    dayCell.CoDutyStatusGlyphNight = statusGlyph;
                    System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Updated Night slot for day {dayNumber}: '{partnerDisplayName}' {statusGlyph}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[RefreshCoDutyFromShared] Refresh completed");
        }

        /// <summary>
        /// Usuwa TYLKO współdyżurnego z zaznaczonych slotów (symbol dyżuru zostaje).
        /// Aktualizacja UI + _sharedDeclarations natychmiastowa, zapis do bazy po "Zapisz".
        /// </summary>
        private void OnRemoveCoDutyPartnerClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            var currentDoctor = ViewModel.SelectedDoctor;
            if (currentDoctor == null)
                return;

            var selectedSlots = ViewModel.SelectedSlots.ToList();
            if (!selectedSlots.Any())
                return;

            System.Diagnostics.Debug.WriteLine($"[RemoveCoDuty] Usuwanie współdyżurnego z {selectedSlots.Count} slotów");

            foreach (var slot in selectedSlots)
            {
                var dayCell = ViewModel.DayCells.FirstOrDefault(c => c.Index == slot.Index);
                if (dayCell == null || !dayCell.InMonth)
                    continue;

                // Wyczyść współdyżurnego w UI
                switch (slot.Part)
                {
                    case SlotPart.Full:
                        dayCell.CoDutyPartnerFull = "";
                        dayCell.CoDutyStatusGlyphFull = "";
                        break;
                    case SlotPart.Day:
                        dayCell.CoDutyPartnerDay = "";
                        dayCell.CoDutyStatusGlyphDay = "";
                        break;
                    case SlotPart.Night:
                        dayCell.CoDutyPartnerNight = "";
                        dayCell.CoDutyStatusGlyphNight = "";
                        break;
                }

                // Wyczyść w _sharedDeclarations
                ViewModel.UpdateCoDutyFields(currentDoctor.Id, dayCell.Date.Day, null, null, null, null);

                System.Diagnostics.Debug.WriteLine($"[RemoveCoDuty] Usunięto współdyżurnego z dnia {dayCell.Date.Day}, slot {slot.Part}");
            }
        }

        /// <summary>
        /// Czyści WSZYSTKO (symbol dyżuru + współdyżurny) z zaznaczonych slotów.
        /// Dla wielu slotów pokazuje dialog potwierdzenia.
        /// Aktualizacja UI + _sharedDeclarations natychmiastowa, zapis do bazy po "Zapisz".
        /// </summary>
        private async void OnClearDeclarationClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            var currentDoctor = ViewModel.SelectedDoctor;
            if (currentDoctor == null)
                return;

            var selectedSlots = ViewModel.SelectedSlots.ToList();
            if (!selectedSlots.Any())
                return;

            // Dialog potwierdzenia dla wielu slotów
            if (selectedSlots.Count > 1)
            {
                var confirmDialog = App.CreateThemedDialog();
                confirmDialog.Title = "Wyczyść deklaracje";
                confirmDialog.Content = $"Czy na pewno wyczyścić deklaracje z {selectedSlots.Count} slotów?";
                confirmDialog.PrimaryButtonText = "Wyczyść";
                confirmDialog.CloseButtonText = "Anuluj";
                confirmDialog.DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close;
                confirmDialog.XamlRoot = this.XamlRoot;

                var result = await confirmDialog.ShowAsync();
                if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                    return; // User anulował
            }

            System.Diagnostics.Debug.WriteLine($"[ClearDeclaration] Czyszczenie {selectedSlots.Count} slotów");

            foreach (var slot in selectedSlots)
            {
                var dayCell = ViewModel.DayCells.FirstOrDefault(c => c.Index == slot.Index);
                if (dayCell == null || !dayCell.InMonth)
                    continue;

                // Wyczyść symbol dyżuru + współdyżurnego w UI
                switch (slot.Part)
                {
                    case SlotPart.Full:
                        dayCell.SymbolFull = "";
                        dayCell.CoDutyPartnerFull = "";
                        dayCell.CoDutyStatusGlyphFull = "";
                        break;
                    case SlotPart.Day:
                        dayCell.SymbolDay = "";
                        dayCell.CoDutyPartnerDay = "";
                        dayCell.CoDutyStatusGlyphDay = "";
                        break;
                    case SlotPart.Night:
                        dayCell.SymbolNight = "";
                        dayCell.CoDutyPartnerNight = "";
                        dayCell.CoDutyStatusGlyphNight = "";
                        break;
                }

                // Wyczyść w _sharedDeclarations (współdyżurny)
                ViewModel.UpdateCoDutyFields(currentDoctor.Id, dayCell.Date.Day, null, null, null, null);

                System.Diagnostics.Debug.WriteLine($"[ClearDeclaration] Wyczyszczono dzień {dayCell.Date.Day}, slot {slot.Part}");
            }

            // Symbol dyżuru zostanie zapisany przez mechanizm dirty tracking w ViewModel
        }

        // Handler dla dynamicznego skalowania czcionek w slotach
        private void SlotContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DayCell dayCell)
            {
                // Aktualizuj wysokość i szerokość slotu w ViewModel
                dayCell.SlotHeight = e.NewSize.Height;
                dayCell.SlotWidth = e.NewSize.Width;
            }
        }
    }
}