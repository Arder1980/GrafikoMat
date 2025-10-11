using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input; // dla InputKeyboardSource/CoreVirtualKeyStates
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices; // P/Invoke GetKeyState
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

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

        // Jednorazowe wytłumienie Tapped/DoubleTapped po Ctrl-toggle
        private bool _suppressNextTapOnce = false;

        // Lokalny stan klawiszy – plan C
        private bool _ctrlDown = false;
        private bool _shiftDown = false;

        // P/Invoke: najpewniejsza detekcja stanu Ctrl/Shift na Desktop
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
                CalendarGridView.AddHandler(PointerPressedEvent, new PointerEventHandler(Calendar_PointerPressed), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerMovedEvent, new PointerEventHandler(Calendar_PointerMoved), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerReleasedEvent, new PointerEventHandler(Calendar_PointerReleased), handledEventsToo: true);
                CalendarGridView.AddHandler(PointerExitedEvent, new PointerEventHandler(Calendar_PointerExited), handledEventsToo: true);
                CalendarGridView.AddHandler(RightTappedEvent, new RightTappedEventHandler(Calendar_RightTapped), handledEventsToo: true);

                // tłumienie pojedynczego wyboru po naszym togglu
                CalendarGridView.AddHandler(TappedEvent, new TappedEventHandler(Calendar_Tapped), handledEventsToo: true);
                CalendarGridView.AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(Calendar_DoubleTapped), handledEventsToo: true);

                // stabilne śledzenie klawiszy na samej siatce
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

        private static bool IsCtrlPressedCombined(VirtualKeyModifiers mods)
        {
            // 1) z eventu
            bool byMods = (mods & VirtualKeyModifiers.Control) == VirtualKeyModifiers.Control;
            // 2) globalnie (WinUI)
            bool byWinUI = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            // 3) natywnie (user32)
            bool byNative = IsCtrlDownNative();
            return byMods || byWinUI || byNative;
        }

        private static bool IsShiftPressedCombined(VirtualKeyModifiers mods)
        {
            bool byMods = (mods & VirtualKeyModifiers.Shift) == VirtualKeyModifiers.Shift;
            bool byWinUI = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool byNative = IsShiftDownNative();
            return byMods || byWinUI || byNative;
        }

        private void Calendar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var pointInfo = e.GetCurrentPoint(CalendarGridView);
            if (!pointInfo.Properties.IsLeftButtonPressed)
                return;

            // Fokus na siatkę – KeyDown/Up zaczną działać po pierwszym kliknięciu
            CalendarGridView.Focus(FocusState.Pointer);

            var (index, slotPart) = GetIndexAndSlotFromPoint(pointInfo.Position);
            if (index == -1 || !ViewModel.DayCells[index].InMonth)
                return;

            var mods = e.KeyModifiers;

            // Detekcja CTRL/SHIFT: 4 źródła (KeyModifiers, WinUI, native, lokalne flagi)
            bool isCtrlPressed = IsCtrlPressedCombined(mods) || _ctrlDown;
            bool isShiftPressed = IsShiftPressedCombined(mods) || _shiftDown;

            e.Handled = true; // selekcją zarządzamy wyłącznie tutaj

            // 1) CTRL – toggle (PRIORYTET). Zero drag, zero CapturePointer.
            if (isCtrlPressed)
            {
                _isDragging = false;

                ViewModel.ToggleSlotSelection(index, slotPart);
                _lastClickedSlot = new SelectedSlot(index, slotPart);

                // Jednorazowo tłumimy ewentualny Tapped/DoubleTapped z kontenera itemu
                _suppressNextTapOnce = true;
                return;
            }

            // 2) SHIFT – zakres od ostatniego kliknięcia
            if (isShiftPressed && _lastClickedSlot != null)
            {
                _isDragging = false;

                ViewModel.SelectDragRange(_lastClickedSlot.Index, index, _lastClickedSlot.Part, slotPart);
                return;
            }

            // 3) Brak modyfikatorów – single + przygotowanie pod drag-select
            _isDragging = true;
            _dragStartIndex = index;
            _dragStartSlotPart = slotPart;
            CalendarGridView.CapturePointer(e.Pointer);

            ViewModel.SelectSingleSlot(index, slotPart);
            _lastClickedSlot = new SelectedSlot(index, slotPart);
        }

        private void Calendar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && _isDragging)
            {
                var point = e.GetCurrentPoint(CalendarGridView).Position;
                var (currentIndex, currentPart) = GetIndexAndSlotFromPoint(point);

                if (currentIndex != -1 && ViewModel.DayCells[currentIndex].InMonth)
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

                // TODO: menu kontekstowe
                e.Handled = true;
            }
        }

        // Jednorazowe tłumienie po CTRL – chroni toggle przed nadpisaniem przez single-select w Tapped
        private void Calendar_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_suppressNextTapOnce)
            {
                _suppressNextTapOnce = false;
                e.Handled = true;
            }
        }

        private void Calendar_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_suppressNextTapOnce)
            {
                _suppressNextTapOnce = false;
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
            if (ViewModel?.DayCells == null) return;
            foreach (var cell in ViewModel.DayCells)
            {
                UpdateCellBrushes(cell);
            }
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
                var transparent = Colors.Transparent;
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
            else // Dark Theme
            {
                var transparent = Colors.Transparent;
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
