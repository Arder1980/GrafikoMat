using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace GrafikoMat.Controls
{
    public sealed partial class ActionContainer : UserControl,
        IRecipient<ShowBusyOverlayMessage>,
        IRecipient<ShowStatusOverlayMessage>,
        IRecipient<HideOverlayMessage>,
        IRecipient<ShowProgressOverlayMessage>,
        IRecipient<UpdateProgressMessage>
    {
        private readonly Guid _viewId = Guid.NewGuid();

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(ActionContainer), new PropertyMetadata(null));

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public ActionContainer()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        public void Receive(ShowBusyOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                // Fade-out zawartości
                FadeOutStoryboard.Begin();

                // Po zakończeniu animacji pokaż overlay
                FadeOutStoryboard.Completed += (s, e) =>
                {
                    ActionProgressRing.IsActive = true;
                    CustomInfoBar.Visibility = Visibility.Collapsed;
                    InfoBarContainer.Opacity = 0;
                    OverlayHost.Visibility = Visibility.Visible;
                };
            });
        }

        public void Receive(ShowStatusOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                // Jeśli overlay nie był widoczny, zrób fade-out zawartości
                if (OverlayHost.Visibility == Visibility.Collapsed)
                {
                    FadeOutStoryboard.Begin();

                    FadeOutStoryboard.Completed += (s, e) =>
                    {
                        ShowStatusMessage(message);
                    };
                }
                else
                {
                    // Overlay już widoczny
                    if (CustomInfoBar.Visibility == Visibility.Visible)
                    {
                        InfoBarFadeOutStoryboard.Begin();
                        InfoBarFadeOutStoryboard.Completed += (s, e) =>
                        {
                            ShowStatusMessage(message);
                        };
                    }
                    else
                    {
                        ShowStatusMessage(message);
                    }
                }
            });
        }

        private void ShowStatusMessage(ShowStatusOverlayMessage message)
        {
            ActionProgressRing.IsActive = false;

            // Pokaż custom InfoBar
            CustomInfoBar.Visibility = Visibility.Visible;

            // Ustaw tekst
            InfoTitle.Text = message.Title;
            InfoMessage.Text = message.Message;

            // Sprawdź motyw
            var isDark = ActualTheme == ElementTheme.Dark ||
                        (ActualTheme == ElementTheme.Default &&
                         Application.Current.RequestedTheme == ApplicationTheme.Dark);

            // Kolory: jasne dla ciemnego motywu, średnie dla jasnego motywu
            switch (message.Severity)
            {
                case InfoBarSeverity.Success:
                    var successColor = isDark
                        ? Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)  // Jasna zieleń (ciemny motyw)
                        : Color.FromArgb(0xFF, 0x16, 0xA3, 0x4A); // Średnia zieleń (jasny motyw)
                    StatusBorder.Background = new SolidColorBrush(successColor);
                    StatusIcon.Foreground = new SolidColorBrush(successColor);
                    StatusIcon.Glyph = "\uF13E"; // CheckmarkCircleFilled
                    break;

                case InfoBarSeverity.Error:
                    var errorColor = isDark
                        ? Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)  // Jasna czerwień (ciemny motyw)
                        : Color.FromArgb(0xFF, 0xDC, 0x26, 0x26); // Średnia czerwień (jasny motyw)
                    StatusBorder.Background = new SolidColorBrush(errorColor);
                    StatusIcon.Foreground = new SolidColorBrush(errorColor);
                    StatusIcon.Glyph = "\uEA39"; // ErrorBadgeFilled
                    break;

                case InfoBarSeverity.Warning:
                    var warningColor = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B); // Pomarańczowy
                    StatusBorder.Background = new SolidColorBrush(warningColor);
                    StatusIcon.Foreground = new SolidColorBrush(warningColor);
                    StatusIcon.Glyph = "\uE7BA"; // Warning
                    break;

                default: // Informational
                    var infoColor = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6); // Niebieski
                    StatusBorder.Background = new SolidColorBrush(infoColor);
                    StatusIcon.Foreground = new SolidColorBrush(infoColor);
                    StatusIcon.Glyph = "\uF167"; // InfoFilled
                    break;
            }

            OverlayHost.Visibility = Visibility.Visible;

            // Fade-in animation
            InfoBarFadeInStoryboard.Begin();
        }

        public void Receive(HideOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                // Ukryj progress overlay jeśli jest widoczny
                if (ProgressOverlay.Visibility == Visibility.Visible)
                {
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                    IndeterminateProgress.IsActive = false;
                    FadeInStoryboard.Begin();
                    return;
                }

                // Fade-out InfoBar
                if (CustomInfoBar.Visibility == Visibility.Visible)
                {
                    InfoBarFadeOutStoryboard.Begin();
                    InfoBarFadeOutStoryboard.Completed += (s, e) =>
                    {
                        OverlayHost.Visibility = Visibility.Collapsed;
                        CustomInfoBar.Visibility = Visibility.Collapsed;
                        ActionProgressRing.IsActive = false;

                        // Fade-in zawartości z powrotem
                        FadeInStoryboard.Begin();
                    };
                }
                else
                {
                    OverlayHost.Visibility = Visibility.Collapsed;
                    ActionProgressRing.IsActive = false;

                    // Fade-in zawartości z powrotem
                    FadeInStoryboard.Begin();
                }
            });
        }

        public void Receive(ShowProgressOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                // Fade-out zawartości
                FadeOutStoryboard.Begin();

                FadeOutStoryboard.Completed += (s, e) =>
                {
                    // Ustaw tytuł i nazwę silnika
                    ProgressTitle.Text = message.Title;
                    ProgressEngineName.Text = $"Silnik: {message.EngineName}";

                    // Pokaż/ukryj przycisk Cancel
                    CancelButton.Visibility = message.IsCancellable ? Visibility.Visible : Visibility.Collapsed;

                    // Wybierz ProgressBar lub ProgressRing
                    if (message.IsIndeterminate)
                    {
                        // Deterministyczne solvery (Backtracking, A*) - ProgressRing
                        IndeterminateProgress.Visibility = Visibility.Visible;
                        IndeterminateProgress.IsActive = true;
                        DeterminateProgress.Visibility = Visibility.Collapsed;
                        DeterminateProgress.Value = 0;
                    }
                    else
                    {
                        // Metaheurystyki - ProgressBar
                        DeterminateProgress.Visibility = Visibility.Visible;
                        DeterminateProgress.Value = 0;
                        IndeterminateProgress.Visibility = Visibility.Collapsed;
                        IndeterminateProgress.IsActive = false;
                    }

                    // Resetuj tekst statusu
                    ProgressStatusText.Text = "Inicjalizacja...";

                    // Pokaż overlay
                    ProgressOverlay.Visibility = Visibility.Visible;
                };
            });
        }

        public void Receive(UpdateProgressMessage message)
        {
            if (message.ViewId != _viewId) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                // Aktualizuj ProgressBar jeśli jest widoczny
                if (DeterminateProgress.Visibility == Visibility.Visible)
                {
                    DeterminateProgress.Value = message.Progress * 100;
                }

                // Aktualizuj tekst statusu jeśli podano
                if (!string.IsNullOrEmpty(message.StatusText))
                {
                    ProgressStatusText.Text = message.StatusText;
                }
            });
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            // Wyślij komunikat anulowania
            WeakReferenceMessenger.Default.Send(new CancelOperationMessage(_viewId));

            // Opcjonalnie: wyłącz przycisk aby zapobiec wielokrotnemu kliknięciu
            CancelButton.IsEnabled = false;
            CancelButton.Content = "Anulowanie...";
        }

        public Guid GetViewId() => _viewId;
    }
}