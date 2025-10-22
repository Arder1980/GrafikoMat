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

    // NOWE: Wiadomości dla długich operacji z progressem
    /// <summary>
    /// Pokazuje nakładkę z ProgressBar/ProgressRing i opisem dla długotrwałych operacji.
    /// </summary>
    /// <param name="IsIndeterminate">Jeśli true, pokazuje ProgressRing (nieskończony spinner). Jeśli false, pokazuje ProgressBar (0-100%).</param>
    public sealed record ShowProgressOverlayMessage(Guid ViewId, string Title, string EngineName, bool IsCancellable, bool IsIndeterminate);

    /// <summary>
    /// Aktualizuje postęp i opis w nakładce progressu.
    /// </summary>
    public sealed record UpdateProgressMessage(Guid ViewId, double Progress, string? StatusText = null);

    /// <summary>
    /// Sygnał anulowania operacji wysłany przez użytkownika (przycisk Cancel).
    /// </summary>
    public sealed record CancelOperationMessage(Guid ViewId);
}