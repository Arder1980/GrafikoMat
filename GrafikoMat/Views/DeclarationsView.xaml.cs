using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
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

        public void OnDeclCloseOnly() => CloseRequested?.Invoke();
        public void OnDeclSaveAndCloseOnly() => SaveAndCloseRequested?.Invoke();

        public DeclarationsViewModel ViewModel => this.DataContext as DeclarationsViewModel;

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
            this.DataContext = vm;
            if (vm != null)
            {
                vm.PropertyChanged += Vm_PropertyChanged;
            }
            UpdateAllCellBrushes();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
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
            // SEKCJA 2: Typy deklaracji - PRZESUNIĘTE W PRAWO
            // ============================================
            var mogeItem = new MenuFlyoutItem
            {
                Text = "        Mogę",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(mogeItem);

            var chceItem = new MenuFlyoutItem
            {
                Text = "        Chcę",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(chceItem);

            var warunkowoItem = new MenuFlyoutItem
            {
                Text = "        Mogę warunkowo",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(warunkowoItem);

            var rezerwacjaItem = new MenuFlyoutItem
            {
                Text = "        Rezerwacja",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(rezerwacjaItem);

            var nieMogeItem = new MenuFlyoutItem
            {
                Text = "        Nie mogę",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(nieMogeItem);

            var innyDyzurItem = new MenuFlyoutItem
            {
                Text = "        Inny dyżur",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(innyDyzurItem);

            var urlopItem = new MenuFlyoutItem
            {
                Text = "        Urlop",
                IsEnabled = false,
                Style = indentedStyle
            };
            contextMenu.Items.Add(urlopItem);

            contextMenu.Items.Add(new MenuFlyoutSeparator());

            // ============================================
            // SEKCJA 3: Współdyżurny z rozwijaną listą
            // ============================================

            // ✅ SPRAWDŹ czy w zaznaczonych slotach są deklaracje
            bool hasDeclarationInSelection = ViewModel.SelectedSlots
                .Any(slot => ViewModel.HasDeclarationInSlot(slot.Index, slot.Part));

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
                            IsEnabled = false  // 🔧 PLACEHOLDER - na razie bez akcji
                        };
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
            // SEKCJA 4: Przełącznik trybu
            // ============================================
            var toggleModeItem = new MenuFlyoutItem
            {
                Text = "Przełącz tryb (12h/24h)",
                IsEnabled = false  // 🔧 PLACEHOLDER
            };
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
                ViewModel.ToggleSlotSelection(index, slotPart, isMultiSelect: true); // ✅ ZMIENIONE
                _lastClickedSlot = new SelectedSlot(index, slotPart);
            }

            e.Handled = true;
        }

        private void CalendarGridView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _ctrlDown = true;
            if (e.Key == VirtualKey.Shift) _shiftDown = true;
        }

        private void CalendarGridView_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Control) _ctrlDown = false;
            if (e.Key == VirtualKey.Shift) _shiftDown = false;
        }

        private void OnThemeChanged(FrameworkElement sender, object args) => UpdateAllCellBrushes();

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
                WinUIBrush effectiveBackground;
                WinUIBrush effectiveBorder;
                WinUIBrush dayNumberFg;
                WinUIBrush headerBg;

                if (cell.IsFullSelected || cell.IsDaySelected || cell.IsNightSelected)
                {
                    effectiveBackground = new WinUISolidColorBrush(WinUIColor.FromArgb(255, 66, 135, 245));
                    effectiveBorder = new WinUISolidColorBrush(WinUIColor.FromArgb(255, 66, 135, 245));
                    dayNumberFg = new WinUISolidColorBrush(WinUIColors.White);
                    headerBg = new WinUISolidColorBrush(WinUIColor.FromArgb(255, 50, 110, 200));
                }
                else if (!cell.InMonth)
                {
                    if (isDark)
                    {
                        effectiveBackground = cell.IsDayOff ?
                            new WinUISolidColorBrush(WinUIColor.FromArgb(40, 60, 60, 60)) :
                            new WinUISolidColorBrush(WinUIColor.FromArgb(40, 50, 50, 50));
                        effectiveBorder = new WinUISolidColorBrush(WinUIColor.FromArgb(10, 255, 255, 255));
                        dayNumberFg = new WinUISolidColorBrush(WinUIColors.White);
                        headerBg = new WinUISolidColorBrush(WinUIColor.FromArgb(40, 70, 70, 70));
                    }
                    else
                    {
                        effectiveBackground = cell.IsDayOff ?
                            new WinUISolidColorBrush(WinUIColor.FromArgb(40, 220, 220, 220)) :
                            new WinUISolidColorBrush(WinUIColor.FromArgb(40, 240, 240, 240));
                        effectiveBorder = new WinUISolidColorBrush(WinUIColor.FromArgb(10, 0, 0, 0));
                        dayNumberFg = new WinUISolidColorBrush(WinUIColors.Black);
                        headerBg = new WinUISolidColorBrush(WinUIColor.FromArgb(40, 230, 230, 230));
                    }
                }
                else
                {
                    if (isDark)
                    {
                        effectiveBackground = cell.IsDayOff ?
                            new WinUISolidColorBrush(WinUIColor.FromArgb(150, 50, 50, 50)) :
                            new WinUISolidColorBrush(WinUIColor.FromArgb(150, 40, 40, 40));
                        effectiveBorder = new WinUISolidColorBrush(WinUIColor.FromArgb(80, 255, 255, 255));
                        dayNumberFg = new WinUISolidColorBrush(WinUIColors.White);
                        headerBg = new WinUISolidColorBrush(WinUIColor.FromArgb(150, 60, 60, 60));
                    }
                    else
                    {
                        effectiveBackground = cell.IsDayOff ?
                            new WinUISolidColorBrush(WinUIColor.FromArgb(150, 235, 235, 235)) :
                            new WinUISolidColorBrush(WinUIColor.FromArgb(150, 250, 250, 250));
                        effectiveBorder = new WinUISolidColorBrush(WinUIColor.FromArgb(80, 0, 0, 0));
                        dayNumberFg = new WinUISolidColorBrush(WinUIColors.Black);
                        headerBg = new WinUISolidColorBrush(WinUIColor.FromArgb(150, 245, 245, 245));
                    }
                }

                cell.EffectiveBackground = effectiveBackground;
                cell.EffectiveBorderBrush = effectiveBorder;
                cell.DayNumberForeground = dayNumberFg;
                cell.EffectiveHeaderBackground = headerBg;
            }
        }
        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= Vm_PropertyChanged;
            }

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
    }
}