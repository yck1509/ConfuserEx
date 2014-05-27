using System;

namespace Confuser.Core.Project {
	/// <summary>
	///     The exception that is thrown when attempted to parse an invalid pattern.
	/// </summary>
	public class InvalidPatternException : Exception {
		/// <summary>
		///     Initializes a new instance of the <see cref="ConfuserException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public InvalidPatternException(string message)
			: base(message) { }

		/// <summary>
		///     Initializes a new instance of the <see cref="ConfuserException" /> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">
		///     The exception that is the cause of the current exception, or a null reference (Nothing in
		///     Visual Basic) if no inner exception is specified.
		/// </param>
		public InvalidPatternException(string message, Exception innerException)
			: base(message, innerException) { }
	}
}