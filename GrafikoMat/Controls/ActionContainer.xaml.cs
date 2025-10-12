using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Windows.Graphics.Effects;
using WinRT;

// Nowy using dla efektów graficznych
using Microsoft.Graphics.Canvas.Effects;

namespace GrafikoMat.Controls
{
    public sealed partial class ActionContainer : UserControl, IRecipient<ShowBusyOverlayMessage>, IRecipient<ShowStatusOverlayMessage>, IRecipient<HideOverlayMessage>
    {
        private readonly Guid _viewId = Guid.NewGuid();
        private Compositor _compositor;
        private SpriteVisual _blurVisual;

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

            // Inicjalizujemy obiekty kompozycji
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _blurVisual = _compositor.CreateSpriteVisual();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            WeakReferenceMessenger.Default.RegisterAll(this);

            // Inicjalizujemy i podpinamy nasz efekt rozmycia
            InitializeBlurEffect();
            ElementCompositionPreview.SetElementChildVisual(BackgroundGrid, _blurVisual);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void InitializeBlurEffect()
        {
            // Krok 1: Definiujemy tylko jeden, prosty efekt - rozmycie gaussowskie.
            var blurEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                // Krok 2: Ustawiamy znacznie mniejszą wartość rozmycia dla subtelniejszego efektu.
                // Możesz eksperymentować z tą wartością (np. 1.0f, 2.0f), aby uzyskać idealny rezultat.
                BlurAmount = 10.0f,
                Source = new CompositionEffectSourceParameter("Backdrop")
            };

            // Krok 3: Tworzymy pędzel bezpośrednio z tego jednego efektu, pomijając mieszanie i kolorowanie.
            var effectFactory = _compositor.CreateEffectFactory(blurEffect);
            var effectBrush = effectFactory.CreateBrush();

            // Ustawiamy źródło dla rozmycia, czyli tło za naszą kontrolką
            effectBrush.SetSourceParameter("Backdrop", _compositor.CreateBackdropBrush());

            _blurVisual.Brush = effectBrush;

            // Dopasowanie rozmiaru wizualizacji do rozmiaru siatki pozostaje bez zmian
            _blurVisual.Size = new Vector2((float)BackgroundGrid.ActualWidth, (float)BackgroundGrid.ActualHeight);
            BackgroundGrid.SizeChanged += (s, e) =>
            {
                if (e.NewSize != e.PreviousSize)
                {
                    _blurVisual.Size = e.NewSize.ToVector2();
                }
            };
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