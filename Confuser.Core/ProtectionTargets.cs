using System;

namespace Confuser.Core {
	/// <summary>
	///     Targets of protection.
	/// </summary>
	[Flags]
	public enum ProtectionTargets {
		/// <summary> Type definitions. </summary>
		Types = 1,

		/// <summary> Method definitions. </summary>
		Methods = 2,

		/// <summary> Field definitions. </summary>
		Fields = 4,

		/// <summary> Event definitions. </summary>
		Events = 8,

		/// <summary> Property definitions. </summary>
		Properties = 16,

		/// <summary> All member definitions (i.e. type, methods, fields, events and properties). </summary>
		AllMembers = Types | Methods | Fields | Events | Properties,

		/// <summary> Module definitions. </summary>
		Modules = 32,

		/// <summary> All definitions (i.e. All member definitions and modules). </summary>
		AllDefinitions = AllMembers | Modules
	}
}