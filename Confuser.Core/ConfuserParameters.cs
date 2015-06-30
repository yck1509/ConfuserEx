using System;
using Confuser.Core.Project;

namespace Confuser.Core {
	/// <summary>
	///     Parameters that passed to <see cref="ConfuserEngine" />.
	/// </summary>
	public class ConfuserParameters {
		/// <summary>
		///     Gets or sets the project that would be processed.
		/// </summary>
		/// <value>The Confuser project.</value>
		public ConfuserProject Project { get; set; }

		/// <summary>
		///     Gets or sets the logger that used to log the protection process.
		/// </summary>
		/// <value>The logger, or <c>null</c> if logging is not needed.</value>
		public ILogger Logger { get; set; }

		internal bool PackerInitiated { get; set; }

		/// <summary>
		///     Gets or sets the plugin discovery service.
		/// </summary>
		/// <value>The plugin discovery service, or <c>null</c> if default discovery is used.</value>
		public PluginDiscovery PluginDiscovery { get; set; }

		/// <summary>
		///     Gets or sets the marker.
		/// </summary>
		/// <value>The marker, or <c>null</c> if default marker is used.</value>
		public Marker Marker { get; set; }

		/// <summary>
		///     Gets the actual non-null logger.
		/// </summary>
		/// <returns>The logger.</returns>
		internal ILogger GetLogger() {
			return Logger ?? NullLogger.Instance;
		}

		/// <summary>
		///     Gets the actual non-null plugin discovery service.
		/// </summary>
		/// <returns>The plugin discovery service.</returns>
		internal PluginDiscovery GetPluginDiscovery() {
			return PluginDiscovery ?? PluginDiscovery.Instance;
		}

		/// <summary>
		///     Gets the actual non-null marker.
		/// </summary>
		/// <returns>The marker.</returns>
		internal Marker GetMarker() {
			return Marker ?? new ObfAttrMarker();
		}
	}
}