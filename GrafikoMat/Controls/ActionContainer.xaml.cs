using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer; // DODANO DLA CLIPBOARD

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
                ErrorActionGrid.Visibility = Visibility.Collapsed; // NOWA LINIA
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

                // NOWA LOGIKA: Pokaż przyciski tylko w przypadku błędu
                if (message.Severity == InfoBarSeverity.Error)
                {
                    ErrorActionGrid.Visibility = Visibility.Visible;
                    ActionInfoBar.IsClosable = false; // Ręczne zamykanie
                }
                else
                {
                    ErrorActionGrid.Visibility = Visibility.Collapsed;
                    ActionInfoBar.IsClosable = true; // Automatyczne zamykanie
                }

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
                ErrorActionGrid.Visibility = Visibility.Collapsed; // NOWA LINIA
            });
        }

        // NOWA METODA: Obsługa kliknięcia przycisku OK
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Wysłanie komunikatu do orkiestratora (dla ewentualnej przyszłej logiki)
            WeakReferenceMessenger.Default.Send(new HideOverlayExplicitlyMessage(_viewId));

            // Ręczne zamknięcie nakładki
            DispatcherQueue.TryEnqueue(() =>
            {
                OverlayHost.Visibility = Visibility.Collapsed;
                ActionInfoBar.IsOpen = false;
            });
        }

        // NOWA METODA: Obsługa kopiowania
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionInfoBar.Severity == InfoBarSeverity.Error && !string.IsNullOrEmpty(ActionInfoBar.Message))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(ActionInfoBar.Title + ": " + ActionInfoBar.Message);
                Clipboard.SetContent(dataPackage);

                // Opcjonalnie: Zmiana tekstu na przycisku na "Skopiowano" na chwilę
                CopyButton.Content = "Skopiowano!";
                await Task.Delay(1000);
                CopyButton.Content = "Kopiuj treść błędu";
            }
        }

        public Guid GetViewId() => _viewId;
    }
}