namespace GrafikoMat.Models
{
    /// <summary>
    /// Wiadomość rozgłaszana w aplikacji, gdy ustawienia (np. silnik, priorytety) zostały zmienione.
    /// </summary>
    public sealed record SettingsHaveChangedMessage;

    /// <summary>
    /// Wiadomość rozgłaszana, gdy dane jednostek lub lekarzy zostały zmodyfikowane w bazie,
    /// co wymaga odświeżenia głównych list w MainViewModel.
    /// </summary>
    public sealed record UnitDataChangedMessage;
}