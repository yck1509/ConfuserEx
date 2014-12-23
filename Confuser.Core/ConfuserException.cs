using System;

namespace Confuser.Core {
	/// <summary>
	///     The exception that is thrown when a handled error occurred during the protection process.
	/// </summary>
	public class ConfuserException : Exception {
		/// <summary>
		///     Initializes a new instance of the <see cref="ConfuserException" /> class.
		/// </summary>
		/// <param name="innerException">The inner exception, or null if no exception is associated with the error.</param>
		public ConfuserException(Exception innerException)
			: base("Exception occurred during the protection process.", innerException) { }
	}
}