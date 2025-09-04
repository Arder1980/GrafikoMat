using GrafikoMat.Common;
using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using GrafikoMat.Views;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window
    {
        private const int MIN_W = 1600;
        private const int MIN_H = 1000;

        private AppWindow? _appWindow;

        public MainViewModel ViewModel { get; }
        public ObservableCollection<UiAction> Actions { get; } = new();
        private readonly DashboardView _dashboardView = new();
        private readonly DeclarationsView _declarationsView = new();
        private Action<DoctorMonthDeclaration>? _evtDeclSave;
        private Action<DoctorMonthDeclaration>? _evtDeclSaveAndClose;
        private Action? _evtDeclClose;

        private bool _isAnimating;
        private bool _isClosing;
        private Storyboard? _activeStoryboard;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProc? _newWndProc;
        private IntPtr _oldWndProc;

        // ZMIANA: Dodajemy flagę, aby ustawić rozmiar tylko raz
        private bool _isInitialSizeSet = false;

        public MainWindow()
        {
            ViewModel = new MainViewModel();
            InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TopBarRow);

            InitAppWindow();
            SetupBackdrop();

            _dashboardView.Attach(ViewModel);
            ViewportCurrent.Content = _dashboardView;
            BuildActionsForDashboard();
            ResetViewportState();

            _evtDeclSave = dm => OnDeclSave(dm);
            _evtDeclSaveAndClose = dm => OnDeclSaveAndClose(dm);
            _evtDeclClose = () => OnDeclCloseOnly();

            _declarationsView.SaveRequested += _evtDeclSave;
            _declarationsView.SaveAndCloseRequested += _evtDeclSaveAndClose;
            _declarationsView.CloseRequested += _evtDeclClose;
            this.SizeChanged += OnWindowSizeChanged;
            this.Activated += OnWindowActivated;
            this.Closed += OnWindowClosed;
        }

        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow is not null)
            {
                _appWindow.Title = string.Empty;
                // ZMIANA: Usunięto ustawianie rozmiaru stąd, bo jest za wcześnie
                // _appWindow.Resize(new SizeInt32(MIN_W, MIN_H)); 
                try { _appWindow.TitleBar.ExtendsContentIntoTitleBar = true; } catch { }

                _appWindow.Changed += OnAppWindowChanged;

                SubclassWindow(hwnd);
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange && !_isClosing)
            {
                SetupBackdrop();
            }
        }

        private void OnWindowActivated(object? sender, WindowActivatedEventArgs e)
        {
            // ZMIANA: Ustawiamy początkowy rozmiar okna tutaj, przy pierwszej aktywacji.
            if (!_isInitialSizeSet && _appWindow != null)
            {
                _appWindow.Resize(new SizeInt32(MIN_W, MIN_H));
                _isInitialSizeSet = true;
            }

            if (_isClosing) return;
            SetupBackdrop();
        }

        private void OnWindowSizeChanged(object? sender, WindowSizeChangedEventArgs e)
        {
            if (_isClosing) return;
        }

        private void SetupBackdrop()
        {
            if (_isClosing) return;
            bool isMaximized = _appWindow?.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Maximized;

            if (!isMaximized)
            {
                try
                {
                    RootGrid.Background = new SolidColorBrush(Colors.Transparent);
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
                catch
                {
                    SystemBackdrop = null;
                    RootGrid.Background = GetLightFallbackBrush();
                }
            }
            else
            {
                SystemBackdrop = null;
                RootGrid.Background = GetLightFallbackBrush();
            }
        }

        private Brush GetLightFallbackBrush()
        {
            if (Application.Current.Resources.TryGetValue("SolidBackgroundFillColorBaseBrush", out var val) && val is Brush b)
                return b;
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF7));
        }

        #region Win32 Interop for Min/Max Size

        private void SubclassWindow(IntPtr hwnd)
        {
            _newWndProc = new WndProc(AppWndProc);
            _oldWndProc = SetWindowLongPtr(hwnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        private IntPtr AppWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var dpi = GetDpiForWindow(hWnd);
                float scalingFactor = dpi / 96f;

                minMaxInfo.ptMinTrackSize.x = (int)(MIN_W * scalingFactor);
                minMaxInfo.ptMinTrackSize.y = (int)(MIN_H * scalingFactor);

                Marshal.StructureToPtr(minMaxInfo, lParam, true);
                return IntPtr.Zero;
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO { public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition; public POINT ptMinTrackSize; public POINT ptMaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        #endregion

        private void BuildActionsForDashboard()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Ustawienia", new RelayCommand(_ => SwitchToSettings())));
            Actions.Add(new UiAction("Dodaj deklaracje dyżurowe", new RelayCommand(_ => SwitchToDeclarations())));
            Actions.Add(new UiAction("Zarządzanie dyżurnymi", new RelayCommand(_ => SwitchToManage())));
            Actions.Add(new UiAction("Generuj grafik", new RelayCommand(_ => GenerateRosterPlaceholder())));
            Actions.Add(new UiAction("Eksportuj...", new RelayCommand(_ => ExportPlaceholder())));
        }

        private void BuildActionsForDeclarations()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Wyczyść zaznaczenie", new RelayCommand(_ => _declarationsView.TriggerClearSelection())));
            Actions.Add(new UiAction("Zapisz", new RelayCommand(_ => _declarationsView.TriggerSave())));
            Actions.Add(new UiAction("Zapisz i zamknij", new RelayCommand(_ => _declarationsView.TriggerSaveAndClose())));
        }

        private void BuildActionsForSettings()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Zapisz ustawienia", new RelayCommand(_ => SaveSettingsPlaceholder())));
        }

        private void BuildActionsForManage()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Dodaj dyżurnego", new RelayCommand(_ => AddDoctorPlaceholder())));
        }

        private async void SwitchToDashboard()
        {
            if (_isClosing || _isAnimating) return;
            _isAnimating = true;

            var sbExit = new Storyboard();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (ViewportCurrent.Content == _declarationsView)
            {
                var leftCol = _declarationsView.LeftColumn;
                var calendar = _declarationsView.CalendarView;

                var calendarOpacityAnim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeOut };
                Storyboard.SetTarget(calendarOpacityAnim, calendar);
                Storyboard.SetTargetProperty(calendarOpacityAnim, "Opacity");
                sbExit.Children.Add(calendarOpacityAnim);

                var leftColOpacityAnim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeOut, BeginTime = TimeSpan.FromMilliseconds(50) };
                Storyboard.SetTarget(leftColOpacityAnim, leftCol);
                Storyboard.SetTargetProperty(leftColOpacityAnim, "Opacity");
                sbExit.Children.Add(leftColOpacityAnim);
            }

            var buttonsOpacityAnim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeOut, BeginTime = TimeSpan.FromMilliseconds(100) };
            Storyboard.SetTarget(buttonsOpacityAnim, ActionButtons);
            Storyboard.SetTargetProperty(buttonsOpacityAnim, "Opacity");
            sbExit.Children.Add(buttonsOpacityAnim);

            var buttonsTranslateAnim = new DoubleAnimation { To = 30, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeOut, BeginTime = TimeSpan.FromMilliseconds(100) };
            Storyboard.SetTarget(buttonsTranslateAnim, ActionButtons);
            Storyboard.SetTargetProperty(buttonsTranslateAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sbExit.Children.Add(buttonsTranslateAnim);

            var tcsExit = new TaskCompletionSource();
            sbExit.Completed += (_, _) => tcsExit.TrySetResult();
            sbExit.Begin();
            await tcsExit.Task;

            _dashboardView.Attach(ViewModel);
            ViewportCurrent.Content = _dashboardView;
            BuildActionsForDashboard();
            ActionButtons.Opacity = 0;
            ActionButtons.RenderTransform = new TranslateTransform { X = -30 };

            var sbEnter = new Storyboard();
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            var newButtonsOpacity = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeIn };
            Storyboard.SetTarget(newButtonsOpacity, ActionButtons);
            Storyboard.SetTargetProperty(newButtonsOpacity, "Opacity");
            sbEnter.Children.Add(newButtonsOpacity);

            var newButtonsTranslate = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(250)), EasingFunction = easeIn };
            Storyboard.SetTarget(newButtonsTranslate, ActionButtons);
            Storyboard.SetTargetProperty(newButtonsTranslate, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sbEnter.Children.Add(newButtonsTranslate);

            var tcsEnter = new TaskCompletionSource();
            sbEnter.Completed += (_, _) => tcsEnter.TrySetResult();
            sbEnter.Begin();
            await tcsEnter.Task;

            _isAnimating = false;
        }

        private async void SwitchToDeclarations()
        {
            if (_isClosing || _isAnimating) return;
            _isAnimating = true;

            var names = ViewModel.DoctorRows.Select(d => d.Name).ToArray();
            _declarationsView.LoadContext(ViewModel.SelectedYear, ViewModel.SelectedMonthIndex, names, 0);

            ActionButtons.Opacity = 0;
            ActionButtons.RenderTransform = new TranslateTransform { X = 30 };

            var leftCol = _declarationsView.LeftColumn;
            var calendar = _declarationsView.CalendarView;
            leftCol.Opacity = 0;
            leftCol.RenderTransform = new TranslateTransform { Y = -20 };
            calendar.Opacity = 0;
            calendar.RenderTransform = new ScaleTransform { ScaleX = 0.95, ScaleY = 0.95 };

            ViewportCurrent.Content = _declarationsView;
            BuildActionsForDeclarations();

            var sb = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var buttonsOpacityAnim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)), EasingFunction = ease };
            Storyboard.SetTarget(buttonsOpacityAnim, ActionButtons);
            Storyboard.SetTargetProperty(buttonsOpacityAnim, "Opacity");
            sb.Children.Add(buttonsOpacityAnim);

            var buttonsTranslateAnim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(300)), EasingFunction = ease };
            Storyboard.SetTarget(buttonsTranslateAnim, ActionButtons);
            Storyboard.SetTargetProperty(buttonsTranslateAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(buttonsTranslateAnim);

            var leftColOpacityAnim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)), EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(100) };
            Storyboard.SetTarget(leftColOpacityAnim, leftCol);
            Storyboard.SetTargetProperty(leftColOpacityAnim, "Opacity");
            sb.Children.Add(leftColOpacityAnim);

            var leftColTranslateAnim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(300)), EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(100) };
            Storyboard.SetTarget(leftColTranslateAnim, leftCol);
            Storyboard.SetTargetProperty(leftColTranslateAnim, "(UIElement.RenderTransform).(TranslateTransform.Y)");
            sb.Children.Add(leftColTranslateAnim);

            var calendarOpacityAnim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(350)), EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(200) };
            Storyboard.SetTarget(calendarOpacityAnim, calendar);
            Storyboard.SetTargetProperty(calendarOpacityAnim, "Opacity");
            sb.Children.Add(calendarOpacityAnim);

            var calendarScaleXAnim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(350)), EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(200) };
            Storyboard.SetTarget(calendarScaleXAnim, calendar);
            Storyboard.SetTargetProperty(calendarScaleXAnim, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            sb.Children.Add(calendarScaleXAnim);

            var calendarScaleYAnim = new DoubleAnimation { To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(350)), EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(200) };
            Storyboard.SetTarget(calendarScaleYAnim, calendar);
            Storyboard.SetTargetProperty(calendarScaleYAnim, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
            sb.Children.Add(calendarScaleYAnim);

            var tcs = new TaskCompletionSource();
            sb.Completed += (_, _) => tcs.TrySetResult();
            sb.Begin();
            await tcs.Task;

            _isAnimating = false;
        }

        private async void SwitchToSettings()
        {
            if (_isClosing) return;
            var v = new TextBlock { Text = "Ustawienia (w przygotowaniu)", Margin = new Thickness(12) };
            await AnimateToAsync(v, forward: true);
            BuildActionsForSettings();
        }

        private async void SwitchToManage()
        {
            if (_isClosing) return;
            var v = new TextBlock { Text = "Zarządzanie dyżurnymi (w przygotowaniu)", Margin = new Thickness(12) };
            await AnimateToAsync(v, forward: true);
            BuildActionsForManage();
        }

        private static void DetachFromParent(FrameworkElement el)
        {
            if (el.Parent is ContentControl cc) cc.Content = null;
            else if (el.Parent is Border b) b.Child = null;
            else if (el.Parent is Panel p) p.Children.Remove(el);
        }

        private static FrameworkElement? TryGetHeader(object? content)
        {
            if (content is FrameworkElement fe)
                return fe.FindName("ViewHeader") as FrameworkElement;
            return null;
        }

        private void ResetViewportState()
        {
            var cur = ViewportCurrent;
            var nxt = ViewportNext;

            var curT = (cur.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            var nxtT = (nxt.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            cur.RenderTransform = curT;
            nxt.RenderTransform = nxtT;

            curT.X = 0;
            nxtT.X = 0;
            cur.Opacity = 1;
            nxt.Opacity = 0;
            var ch = TryGetHeader(cur.Content);
            var nh = TryGetHeader(nxt.Content);

            if (ch != null)
            {
                var t = (ch.RenderTransform as TranslateTransform) ?? new TranslateTransform();
                t.X = 0;
                ch.RenderTransform = t;
            }
            if (nh != null)
            {
                var t = (nh.RenderTransform as TranslateTransform) ?? new TranslateTransform();
                t.X = 0;
                nh.RenderTransform = t;
            }
        }

        private async Task AnimateToAsync(FrameworkElement nextView, bool forward)
        {
            if (_isClosing) { ViewportCurrent.Content = nextView; ResetViewportState(); return; }
            if (ReferenceEquals(ViewportCurrent.Content, nextView))
                return;
            if (_isAnimating)
            {
                DetachFromParent(nextView);
                _activeStoryboard?.Stop();
                _activeStoryboard = null;

                ViewportNext.Content = null;
                ViewportCurrent.Content = nextView;
                ResetViewportState();
                return;
            }

            _isAnimating = true;

            var curPresenter = ViewportCurrent;
            var nxtPresenter = ViewportNext;

            DetachFromParent(nextView);

            ResetViewportState();
            var curTransform = (curPresenter.RenderTransform as TranslateTransform)!;
            var nxtTransform = (nxtPresenter.RenderTransform as TranslateTransform)!;

            var curHeader = TryGetHeader(curPresenter.Content);
            var nxtHeader = TryGetHeader(nextView);
            TranslateTransform? curHeaderTransform = null;
            TranslateTransform? nxtHeaderTransform = null;

            if (curHeader != null)
            {
                curHeaderTransform = (curHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform();
                curHeader.RenderTransform = curHeaderTransform;
            }
            if (nxtHeader != null)
            {
                nxtHeaderTransform = (nxtHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform();
                nxtHeader.RenderTransform = nxtHeaderTransform;
            }

            double offset = 64;
            double fromNext = forward ? +offset : -offset;
            double toCur = forward ? -offset : +offset;

            nxtTransform.X = fromNext;
            nxtPresenter.Opacity = 0;
            curTransform.X = 0;
            curPresenter.Opacity = 1;

            if (nxtHeaderTransform != null) nxtHeaderTransform.X = fromNext * 0.5;
            if (curHeaderTransform != null) curHeaderTransform.X = 0;

            nxtPresenter.Content = nextView;

            var sb = new Storyboard();
            _activeStoryboard = sb;
            var dur = TimeSpan.FromMilliseconds(280);

            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var curX = new DoubleAnimation { From = 0, To = toCur, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curX, curPresenter);
            Storyboard.SetTargetProperty(curX, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(curX);

            var curOp = new DoubleAnimation { From = 1, To = 0, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curOp, curPresenter);
            Storyboard.SetTargetProperty(curOp, "Opacity");
            sb.Children.Add(curOp);

            var nxtX = new DoubleAnimation { From = fromNext, To = 0, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtX, nxtPresenter);
            Storyboard.SetTargetProperty(nxtX, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(nxtX);

            var nxtOp = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtOp, nxtPresenter);
            Storyboard.SetTargetProperty(nxtOp, "Opacity");
            sb.Children.Add(nxtOp);

            if (curHeaderTransform != null)
            {
                var curHX = new DoubleAnimation { From = 0, To = toCur * 0.5, Duration = dur, EasingFunction = easeIn };
                Storyboard.SetTarget(curHX, curHeader);
                Storyboard.SetTargetProperty(curHX, "(UIElement.RenderTransform).(TranslateTransform.X)");
                sb.Children.Add(curHX);
            }

            if (nxtHeaderTransform != null)
            {
                var nxtHX = new DoubleAnimation { From = fromNext * 0.5, To = 0, Duration = dur, EasingFunction = easeOut };
                Storyboard.SetTarget(nxtHX, nxtHeader);
                Storyboard.SetTargetProperty(nxtHX, "(UIElement.RenderTransform).(TranslateTransform.X)");
                sb.Children.Add(nxtHX);
            }

            var tcs = new TaskCompletionSource<bool>();
            sb.Completed += (_, __) =>
            {
                _activeStoryboard = null;
                tcs.TrySetResult(true);
            };

            try
            {
                sb.Begin();
                await tcs.Task;
            }
            catch { }

            if (_isClosing) return;
            ViewportNext.Content = null;
            ViewportCurrent.Content = nextView;

            ResetViewportState();
            _isAnimating = false;
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _isClosing = true;
            try { _activeStoryboard?.Stop(); } catch { }
            _activeStoryboard = null;
            _isAnimating = false;

            this.SizeChanged -= OnWindowSizeChanged;
            this.Activated -= OnWindowActivated;
            this.Closed -= OnWindowClosed;

            if (_appWindow != null) _appWindow.Changed -= OnAppWindowChanged;

            if (_evtDeclSave != null) _declarationsView.SaveRequested -= _evtDeclSave;
            if (_evtDeclSaveAndClose != null) _declarationsView.SaveAndCloseRequested -= _evtDeclSaveAndClose;
            if (_evtDeclClose != null) _declarationsView.CloseRequested -= _evtDeclClose;

            try { ViewportNext.Content = null; } catch { }
            try { ViewportCurrent.Content = null; } catch { }

            try { SystemBackdrop = null; } catch { }
            try { RootGrid.Background = GetLightFallbackBrush(); } catch { }
        }

        private void OnDeclSave(DoctorMonthDeclaration dm)
        {
            if (!string.IsNullOrWhiteSpace(dm.Doctor))
                ViewModel.ApplyDoctorMonth(dm);
        }

        private void OnDeclSaveAndClose(DoctorMonthDeclaration dm)
        {
            OnDeclSave(dm);
            SwitchToDashboard();
        }

        private void OnDeclCloseOnly() => SwitchToDashboard();
        private async void GenerateRosterPlaceholder()
            => await ShowInfo("Generuj grafik", "Tu będzie wywołanie algorytmu generowania grafiku oraz podgląd wyniku w prawej kolumnie.");
        private async void ExportPlaceholder()
            => await ShowInfo("Eksport", "Tu dodamy eksport do XLSX/PDF (np. ClosedXML + szablony).");
        private async void SaveSettingsPlaceholder()
            => await ShowInfo("Ustawienia", "Zapis ustawień (tryb 12h/24h, motyw, itp.) – w przygotowaniu.");
        private async void AddDoctorPlaceholder()
            => await ShowInfo("Dodaj dyżurnego", "Formularz dodania/edycji dyżurnego – w przygotowaniu.");
        private async Task ShowInfo(string title, string message)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK",
                XamlRoot = RootGrid.XamlRoot
            };
            await dlg.ShowAsync();
        }
    }

    public sealed class UiAction
    {
        public string Label { get; }
        public ICommand Command { get; }
        public UiAction(string label, ICommand command) { Label = label; Command = command; }
    }
}