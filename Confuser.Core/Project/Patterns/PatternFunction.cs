using System;
using System.Collections.Generic;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A pattern function.
	/// </summary>
	public abstract class PatternFunction : PatternExpression {
		/// <summary>
		///     Gets the name of function.
		/// </summary>
		/// <value>The name.</value>
		public abstract string Name { get; }

		/// <summary>
		///     Gets the number of arguments of the function.
		/// </summary>
		/// <value>The number of arguments.</value>
		public abstract int ArgumentCount { get; }

		/// <summary>
		///     Gets or sets the arguments of function.
		/// </summary>
		/// <value>The arguments.</value>
		public IList<PatternExpression> Arguments { get; set; }

		/// <inheritdoc />
		public override void Serialize(IList<PatternToken> tokens) {
			tokens.Add(new PatternToken(TokenType.Identifier, Name));
			tokens.Add(new PatternToken(TokenType.LParens));
			for (int i = 0; i < Arguments.Count; i++) {
				if (i != 0)
					tokens.Add(new PatternToken(TokenType.Comma));
				Arguments[i].Serialize(tokens);
			}
			tokens.Add(new PatternToken(TokenType.RParens));
		}
	}
}