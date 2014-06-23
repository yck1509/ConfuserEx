using System;

namespace Confuser.Core {
	/// <summary>
	///     Base class of Confuser protections.
	/// </summary>
	/// <remarks>
	///     A parameterless constructor must exists in derived classes to enable plugin discovery.
	/// </remarks>
	public abstract class Protection : ConfuserComponent {
		/// <summary>
		///     Gets the preset this protection is in.
		/// </summary>
		/// <value>The protection's preset.</value>
		public abstract ProtectionPreset Preset { get; }
	}
}