using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// A registry of different services provided by protections
    /// </summary>
    public class ServiceRegistry : IServiceProvider
    {
        Dictionary<Type, object> services = new Dictionary<Type, object>();
        HashSet<string> serviceIds = new HashSet<string>();

        /// <inheritdoc/>
        public object GetService(Type serviceType)
        {
            return services.GetValueOrDefault(serviceType, null);
        }

        /// <summary>
        /// Register the service with specified ID .
        /// </summary>
        /// <param name="serviceId">The service identifier.</param>
        /// <param name="serviceType">The service type.</param>
        /// <param name="service">The service.</param>
        /// <exception cref="System.ArgumentException">Service with same ID or type has already registered.</exception>
        public void RegisterService(string serviceId, Type serviceType, object service)
        {
            if (!serviceIds.Add(serviceId))
                throw new ArgumentException("Service with ID '" + serviceIds + "' has already registered.", "serviceId");
            if (services.ContainsKey(serviceType))
                throw new ArgumentException("Service with type '" + service.GetType().Name + "' has already registered.", "service");
            services.Add(serviceType, service);
        }

        /// <summary>
        /// Determines whether the service with specified identifier has already registered.
        /// </summary>
        /// <param name="serviceId">The service identifier.</param>
        /// <returns><c>true</c> if the service with specified identifier has already registered; otherwise, <c>false</c>.</returns>
        public bool Contains(string serviceId)
        {
            return serviceIds.Contains(serviceId);
        }
    }
}
