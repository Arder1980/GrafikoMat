using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Services;

    public interface INavigationService
    {
        void Initialize(Frame frame);
        void NavigateTo(string pageKey, object? parameter = null);
        void NavigateTo(Page page);
        bool GoBack();
    }