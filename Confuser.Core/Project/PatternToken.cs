using System;

namespace Confuser.Core.Project {
	/// <summary>
	///     The type of pattern tokens
	/// </summary>
	public enum TokenType {
		/// <summary>
		///     An identifier, could be functions/operators.
		/// </summary>
		Identifier,

		/// <summary>
		///     A string literal.
		/// </summary>
		Literal,

		/// <summary>
		///     A left parenthesis.
		/// </summary>
		LParens,

		/// <summary>
		///     A right parenthesis.
		/// </summary>
		RParens,

		/// <summary>
		///     A comma.
		/// </summary>
		Comma
	}


	/// <summary>
	///     Represent a token in pattern
	/// </summary>
	public struct PatternToken {
		/// <summary>
		///     The position of this token in the pattern, or null if position not available.
		/// </summary>
		public readonly int? Position;

		/// <summary>
		///     The type of this token.
		/// </summary>
		public readonly TokenType Type;

		/// <summary>
		///     The value of this token, applicable to identifiers and literals.
		/// </summary>
		public readonly string Value;

		/// <summary>
		///     Initializes a new instance of the <see cref="PatternToken" /> struct.
		/// </summary>
		/// <param name="pos">The position of token.</param>
		/// <param name="type">The type of token.</param>
		public PatternToken(int pos, TokenType type) {
			Position = pos;
			Type = type;
			Value = null;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="PatternToken" /> struct.
		/// </summary>
		/// <param name="pos">The position of token.</param>
		/// <param name="type">The type of token.</param>
		/// <param name="value">The value of token.</param>
		public PatternToken(int pos, TokenType type, string value) {
			Position = pos;
			Type = type;
			Value = value;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="PatternToken" /> struct.
		/// </summary>
		/// <param name="type">The type of token.</param>
		public PatternToken(TokenType type) {
			Position = null;
			Type = type;
			Value = null;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="PatternToken" /> struct.
		/// </summary>
		/// <param name="type">The type of token.</param>
		/// <param name="value">The value of token.</param>
		public PatternToken(TokenType type, string value) {
			Position = null;
			Type = type;
			Value = value;
		}

		/// <inheritdoc />
		public override string ToString() {
			if (Position != null) {
				if (Value != null)
					return string.Format("[{0}] {1} @ {2}", Type, Value, Position);
				return string.Format("[{0}] @ {1}", Type, Position);
			}
			if (Value != null)
				return string.Format("[{0}] {1}", Type, Value);
			return string.Format("[{0}]", Type);
		}
	}
}