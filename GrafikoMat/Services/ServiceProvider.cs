using System;
using System.Collections.Generic;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Prosty lokalizator serwisów do przechowywania globalnych instancji.
    /// Thread-safe dzięki lock na _lock.
    /// </summary>
    public static class ServiceProvider
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static readonly object _lock = new();

        public static void Register<T>(T service) where T : class
        {
            lock (_lock)
            {
                _services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
            }
        }

        public static T GetService<T>() where T : class
        {
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(T), out var service))
                {
                    return (T)service;
                }
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }
        }
    }
}