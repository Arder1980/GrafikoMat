using System;
using System.Collections.Generic;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Prosty lokalizator serwisów do przechowywania globalnych instancji.
    /// </summary>
    public static class ServiceProvider
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
    }
}