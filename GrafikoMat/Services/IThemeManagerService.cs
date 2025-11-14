using Microsoft.UI.Xaml;

namespace GrafikoMat.Services
{
    public interface IThemeManagerService
    {
        ElementTheme CurrentTheme { get; }
        void Initialize(ElementTheme initialTheme);
        void SetTheme(ElementTheme newTheme);
    }
}
