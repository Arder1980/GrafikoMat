using Microsoft.UI.Xaml.Controls;
using System;

namespace GrafikoMat.Controls
{
    // Wiadomość nakazująca pokazanie nakładki z wskaźnikiem ładowania
    public sealed record ShowBusyOverlayMessage(Guid ViewId);

    // Wiadomość nakazująca pokazanie komunikatu o statusie
    public sealed record ShowStatusOverlayMessage(Guid ViewId, string Title, string Message, InfoBarSeverity Severity);

    // Wiadomość nakazująca ukrycie nakładki
    public sealed record HideOverlayMessage(Guid ViewId);

    // NOWA: Wiadomość rozsyłana przez ActionContainer po kliknięciu 'OK', by poinformować o ręcznym zamknięciu
    public sealed record HideOverlayExplicitlyMessage(Guid ViewId);
}