using System;

namespace Confuser.Core {
	/// <summary>
	///     The exception that is thrown when supposedly unreachable code is executed.
	/// </summary>
	public class UnreachableException : SystemException {
		/// <summary>
		///     Initializes a new instance of the <see cref="UnreachableException" /> class.
		/// </summary>
		public UnreachableException() :
			base("Unreachable code reached.") { }
	}
}