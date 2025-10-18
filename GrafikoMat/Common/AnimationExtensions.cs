using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace GrafikoMat.Common // Upewnij się, że namespace pasuje do folderu
{
    /// <summary>
    /// Zawiera metody rozszerzające dla animacji WinUI.
    /// </summary>
    public static class AnimationExtensions
    {
        /// <summary>
        /// Ustawia cel i właściwość dla animacji DoubleAnimation (fluent syntax).
        /// </summary>
        public static DoubleAnimation SetTarget(this DoubleAnimation animation, DependencyObject target, string propertyPath)
        {
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyPath);
            return animation;
        }
    }
}