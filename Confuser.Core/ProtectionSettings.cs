using System;
using System.Collections.Generic;

namespace Confuser.Core {
	/// <summary>
	///     Protection settings for a certain component
	/// </summary>
	public class ProtectionSettings : Dictionary<ConfuserComponent, Dictionary<string, string>> {
		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionSettings" /> class.
		/// </summary>
		public ProtectionSettings() { }

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionSettings" /> class
		///     from an existing <see cref="ProtectionSettings" />.
		/// </summary>
		/// <param name="settings">The settings to copy from.</param>
		public ProtectionSettings(ProtectionSettings settings) {
			if (settings == null)
				return;

			foreach (var i in settings)
				Add(i.Key, new Dictionary<string, string>(i.Value));
		}

		/// <summary>
		///     Determines whether the settings is empty.
		/// </summary>
		/// <returns><c>true</c> if the settings is empty; otherwise, <c>false</c>.</returns>
		public bool IsEmpty() {
			return Count == 0;
		}
	}
}