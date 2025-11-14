using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media.Animation;

namespace GrafikoMat.Services;

public class NavigationService : INavigationService
{
    private Frame? _frame;
    private readonly Dictionary<string, Type> _pages = new();

    public void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public void Configure(string key, Type pageType)
    {
        lock (_pages)
        {
            if (_pages.ContainsKey(key))
            {
                throw new ArgumentException($"The key {key} is already configured.");
            }
            _pages.Add(key, pageType);
        }
    }

    public bool GoBack()
    {
        if (_frame != null && _frame.CanGoBack)
        {
            _frame.GoBack();
            return true;
        }
        return false;
    }

    public void NavigateTo(string pageKey, object? parameter = null)
    {
        if (_frame == null)
        {
            throw new InvalidOperationException("The navigation service has not been initialized.");
        }

        lock (_pages)
        {
            if (!_pages.ContainsKey(pageKey))
            {
                throw new ArgumentException($"The page key {pageKey} is not configured.");
            }

            var pageType = _pages[pageKey];
            _frame.Navigate(pageType, parameter, new DrillInNavigationTransitionInfo());
        }
    }

    public void NavigateTo(Page page)
    {
        if (_frame == null)
        {
            throw new InvalidOperationException("The navigation service has not been initialized.");
        }
        _frame.Navigate(page.GetType(), null, new DrillInNavigationTransitionInfo());
    }
}