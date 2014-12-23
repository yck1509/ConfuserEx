using System;
using System.Collections.Generic;

namespace Confuser.Core {
	/// <summary>
	///     A registry of different services provided by protections
	/// </summary>
	public class ServiceRegistry : IServiceProvider {
		readonly HashSet<string> serviceIds = new HashSet<string>();
		readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

		/// <inheritdoc />
		object IServiceProvider.GetService(Type serviceType) {
			return services.GetValueOrDefault(serviceType, null);
		}

		/// <summary>
		///     Retrieves the service of type <typeparamref name="T" />.
		/// </summary>
		/// <typeparam name="T">The type of service.</typeparam>
		/// <returns>The service instance.</returns>
		public T GetService<T>() {
			return (T)services.GetValueOrDefault(typeof(T), null);
		}

		/// <summary>
		///     Registers the service with specified ID .
		/// </summary>
		/// <param name="serviceId">The service identifier.</param>
		/// <param name="serviceType">The service type.</param>
		/// <param name="service">The service.</param>
		/// <exception cref="System.ArgumentException">Service with same ID or type has already registered.</exception>
		public void RegisterService(string serviceId, Type serviceType, object service) {
			if (!serviceIds.Add(serviceId))
				throw new ArgumentException("Service with ID '" + serviceIds + "' has already registered.", "serviceId");
			if (services.ContainsKey(serviceType))
				throw new ArgumentException("Service with type '" + service.GetType().Name + "' has already registered.", "service");
			services.Add(serviceType, service);
		}

		/// <summary>
		///     Determines whether the service with specified identifier has already registered.
		/// </summary>
		/// <param name="serviceId">The service identifier.</param>
		/// <returns><c>true</c> if the service with specified identifier has already registered; otherwise, <c>false</c>.</returns>
		public bool Contains(string serviceId) {
			return serviceIds.Contains(serviceId);
		}
	}
}