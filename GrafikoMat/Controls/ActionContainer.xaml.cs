using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI; // <-- Ta linia została dodana

namespace GrafikoMat.Controls
{
    public sealed partial class ActionContainer : UserControl, IRecipient<ShowBusyOverlayMessage>, IRecipient<ShowStatusOverlayMessage>, IRecipient<HideOverlayMessage>
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
            // Najpierw wyrejestruj, aby uniknąć podwójnej subskrypcji.
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
                ActionProgressRing.IsActive = true;
                ActionInfoBar.IsOpen = false;
                OverlayHost.Visibility = Visibility.Visible;
            });
        }

        public void Receive(ShowStatusOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                ActionProgressRing.IsActive = false;
                ActionInfoBar.Title = message.Title;
                ActionInfoBar.Message = message.Message;
                ActionInfoBar.Severity = message.Severity;

                // ================== ZMIANA: Użycie nowego pędzla ==================
                var foregroundBrush = new SolidColorBrush(Colors.White);

                switch (message.Severity)
                {
                    case InfoBarSeverity.Success:
                        ActionInfoBar.Background = (Brush)this.Resources["SuccessBrush"];
                        ActionInfoBar.Foreground = foregroundBrush;
                        break;
                    case InfoBarSeverity.Error:
                        ActionInfoBar.Background = (Brush)this.Resources["ErrorBrush"];
                        ActionInfoBar.Foreground = foregroundBrush;
                        break;
                    default:
                        // Dla innych typów (Warning, Informational) wracamy do domyślnych kolorów motywu
                        ActionInfoBar.ClearValue(Control.BackgroundProperty);
                        ActionInfoBar.ClearValue(Control.ForegroundProperty);
                        break;
                }
                // =================== KONIEC ZMIANY ===================

                ActionInfoBar.IsOpen = true;
                OverlayHost.Visibility = Visibility.Visible;
            });
        }

        public void Receive(HideOverlayMessage message)
        {
            if (message.ViewId != _viewId) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                OverlayHost.Visibility = Visibility.Collapsed;
                ActionInfoBar.IsOpen = false;
                ActionProgressRing.IsActive = false;
            });
        }

        public Guid GetViewId() => _viewId;
    }
}