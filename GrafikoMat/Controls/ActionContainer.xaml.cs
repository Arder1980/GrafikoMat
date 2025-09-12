using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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
            // ZMIANA: Zdarzenie Unloaded jest teraz prawidłowo podpięte do wyrejestrowania
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        // ZMIANA: Nowa, bezpieczna metoda obsługi zdarzenia Loaded
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Najpierw wyrejestruj, aby uniknąć podwójnej subskrypcji.
            // To sprawia, że operacja jest bezpieczna do wielokrotnego wywołania.
            WeakReferenceMessenger.Default.UnregisterAll(this);
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        // ZMIANA: Nowa metoda do obsługi Unloaded dla czystości kodu
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