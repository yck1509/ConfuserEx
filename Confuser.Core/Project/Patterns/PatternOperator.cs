using System;
using System.Collections.Generic;

namespace Confuser.Core.Project.Patterns {
	/// <summary>
	///     A pattern operator.
	/// </summary>
	public abstract class PatternOperator : PatternExpression {
		/// <summary>
		///     Gets the name of operator.
		/// </summary>
		/// <value>The name.</value>
		public abstract string Name { get; }

		/// <summary>
		///     Gets a value indicating whether this is an unary operator.
		/// </summary>
		/// <value><c>true</c> if this is an unary operator; otherwise, <c>false</c>.</value>
		public abstract bool IsUnary { get; }

		/// <summary>
		///     Gets or sets the first operand.
		/// </summary>
		/// <value>The first operand.</value>
		public PatternExpression OperandA { get; set; }

		/// <summary>
		///     Gets or sets the second operand.
		/// </summary>
		/// <value>The second operand.</value>
		public PatternExpression OperandB { get; set; }

		/// <inheritdoc />
		public override void Serialize(IList<PatternToken> tokens) {
			if (IsUnary) {
				tokens.Add(new PatternToken(TokenType.Identifier, Name));
				OperandA.Serialize(tokens);
			}
			else {
				OperandA.Serialize(tokens);
				tokens.Add(new PatternToken(TokenType.Identifier, Name));
				OperandB.Serialize(tokens);
			}
		}
	}
}