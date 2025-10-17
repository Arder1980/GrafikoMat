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
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

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
                UpdateAllCellBrushes();
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
                        if (!cellVM.IsSplit)
                        {
                            return (i, SlotPart.Full);
                        }
                        else
                        {
                            var relativeY = p.Y - bounds.Top;
                            var headerHeight = 24;
                            var contentHeight = container.ActualHeight - headerHeight;
                            if (relativeY < headerHeight) return (i, SlotPart.Day);
                            var part = (relativeY < headerHeight + contentHeight / 2) ? SlotPart.Day : SlotPart.Night;
                            return (i, part);
                        }
                    }
                }
            }
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

            // CTRL - toggle bez drag
            if (isCtrlPressed)
            {
                _isDragging = false;
                _handledCtrlClickInPointerPressed = true; // Oznacz że obsłużyliśmy
                ViewModel.ToggleSlotSelection(index, slotPart);
                _lastClickedSlot = new SelectedSlot(index, slotPart);
                e.Handled = true; // Blokuj tylko dla Ctrl
                return;
            }

            // SHIFT - zakres
            if (isShiftPressed && _lastClickedSlot != null)
            {
                _isDragging = false;
                ViewModel.SelectDragRange(_lastClickedSlot.Index, index, _lastClickedSlot.Part, slotPart);
                e.Handled = true; // Blokuj dla Shift
                return;
            }

            // Brak modyfikatorów - single + drag
            _isDragging = true;
            _dragStartIndex = index;
            _dragStartSlotPart = slotPart;
            CalendarGridView.CapturePointer(e.Pointer);

            ViewModel.SelectSingleSlot(index, slotPart);
            _lastClickedSlot = new SelectedSlot(index, slotPart);
            e.Handled = true; // Blokuj dla drag
        }

        private void Calendar_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // ZMIANA: Tapped jest safety net tylko dla Ctrl+Click
            // Jeśli PointerPressed obsłużył Ctrl, ignore
            if (_handledCtrlClickInPointerPressed)
            {
                _handledCtrlClickInPointerPressed = false; // Reset flagi
                e.Handled = true;
                return;
            }

            // Sprawdź stan Ctrl globalnie (bez KeyModifiers z eventu)
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
                ViewModel.ToggleSlotSelection(index, slotPart);
                _lastClickedSlot = new SelectedSlot(index, slotPart);
            }

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

            var tappedCell = (index >= 0 && index < ViewModel.DayCells.Count) ? ViewModel.DayCells[index] : null;

            if (tappedCell != null && tappedCell.InMonth)
            {
                var currentSelection = new SelectedSlot(index, slotPart);
                if (!ViewModel.SelectedSlots.Contains(currentSelection))
                {
                    ViewModel.SelectSingleSlot(index, slotPart);
                    _lastClickedSlot = currentSelection;
                }

                // TODO: Wyświetl menu kontekstowe
                e.Handled = true;
            }
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

        private void UpdateAllCellBrushes()
        {
            if (ViewModel?.DayCells == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateAllCellBrushes: ViewModel lub DayCells są null!");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"UpdateAllCellBrushes: Aktualizuję {ViewModel.DayCells.Count} komórek");

            foreach (var cell in ViewModel.DayCells)
            {
                UpdateCellBrushes(cell);
            }
        }

        public void RefreshCellBrushes()
        {
            System.Diagnostics.Debug.WriteLine("RefreshCellBrushes wywołane");
            UpdateAllCellBrushes();
        }

        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.PropertyChanged -= Vm_PropertyChanged;
            this.ActualThemeChanged -= OnThemeChanged;
            this.Unloaded -= OnDeclarationsViewUnloaded;

            CalendarGridView.KeyDown -= CalendarGridView_KeyDown;
            CalendarGridView.KeyUp -= CalendarGridView_KeyUp;
        }

        private void UpdateCellBrushes(DayCell cell)
        {
            var currentTheme = ThemeManagerService.Instance.CurrentTheme;

            if (currentTheme == ElementTheme.Light)
            {
                var shadeActiveDay = Color.FromArgb(0x0D, 0, 0, 0);
                var shadeDayOff = Color.FromArgb(0x26, 0, 0, 0);
                var headerBgActive = Color.FromArgb(0x59, 0, 0, 0);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 0, 0, 0);
                var borderLight = Color.FromArgb(0x4D, 0, 0, 0);
                var borderOther = Color.FromArgb(0x0D, 0, 0, 0);
                var textNormal = Color.FromArgb(0xBF, 0, 0, 0);
                var textMuted = Color.FromArgb(0x66, 0, 0, 0);

                Color bgColor, borderColor, numFgColor, headerBgColor;
                if (cell.InMonth)
                {
                    bgColor = cell.IsDayOff ? shadeDayOff : shadeActiveDay;
                    borderColor = borderLight;
                }
                else
                {
                    bgColor = Color.FromArgb(0x05, 0, 0, 0);
                    borderColor = borderOther;
                }

                headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as SolidColorBrush)!.Color = bgColor;
                (cell.EffectiveBorderBrush as SolidColorBrush)!.Color = borderColor;
                (cell.DayNumberForeground as SolidColorBrush)!.Color = numFgColor;
                (cell.EffectiveHeaderBackground as SolidColorBrush)!.Color = headerBgColor;
            }
            else
            {
                var shadeActiveDay = Color.FromArgb(0x0D, 255, 255, 255);
                var shadeDayOff = Color.FromArgb(0x26, 255, 255, 255);
                var headerBgActive = Color.FromArgb(0x59, 255, 255, 255);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 255, 255, 255);
                var borderLight = Color.FromArgb(0x4D, 255, 255, 255);
                var borderOther = Color.FromArgb(0x0D, 255, 255, 255);
                var textNormal = Color.FromArgb(0xE6, 255, 255, 255);
                var textMuted = Color.FromArgb(0x66, 255, 255, 255);

                Color bgColor, borderColor, numFgColor, headerBgColor;
                if (cell.InMonth)
                {
                    bgColor = cell.IsDayOff ? shadeDayOff : shadeActiveDay;
                    borderColor = borderLight;
                }
                else
                {
                    bgColor = Color.FromArgb(0x05, 255, 255, 255);
                    borderColor = borderOther;
                }

                headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as SolidColorBrush)!.Color = bgColor;
                (cell.EffectiveBorderBrush as SolidColorBrush)!.Color = borderColor;
                (cell.DayNumberForeground as SolidColorBrush)!.Color = numFgColor;
                (cell.EffectiveHeaderBackground as SolidColorBrush)!.Color = headerBgColor;
            }

            cell.NotifyBrushUpdate();
        }
    }
}