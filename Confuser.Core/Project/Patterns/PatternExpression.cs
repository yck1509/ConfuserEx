using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A pattern expression.
	/// </summary>
	public abstract class PatternExpression {
		/// <summary>
		///     Evaluates the expression on the specified definition.
		/// </summary>
		/// <param name="definition">The definition.</param>
		/// <returns>The result value.</returns>
		public abstract object Evaluate(IDnlibDef definition);

		/// <summary>
		///     Serializes the expression into tokens.
		/// </summary>
		/// <param name="tokens">The output list of tokens.</param>
		public abstract void Serialize(IList<PatternToken> tokens);
	}
}