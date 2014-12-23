using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A literal expression.
	/// </summary>
	public class LiteralExpression : PatternExpression {
		/// <summary>
		///     Initializes a new instance of the <see cref="LiteralExpression" /> class.
		/// </summary>
		/// <param name="literal">The literal.</param>
		public LiteralExpression(object literal) {
			Literal = literal;
		}

		/// <summary>
		///     Gets the value of literal.
		/// </summary>
		/// <value>The value of literal.</value>
		public object Literal { get; private set; }

		/// <inheritdoc />
		public override object Evaluate(IDnlibDef definition) {
			return Literal;
		}

		/// <inheritdoc />
		public override void Serialize(IList<PatternToken> tokens) {
			if (Literal is bool) {
				var value = (bool)Literal;
				tokens.Add(new PatternToken(TokenType.Identifier, value.ToString().ToLowerInvariant()));
			}
			else
				tokens.Add(new PatternToken(TokenType.Literal, Literal.ToString()));
		}
	}
}