using System;

namespace Confuser.DynCipher.AST {
	public class LiteralExpression : Expression {
		public uint Value { get; set; }

		public static implicit operator LiteralExpression(uint val) {
			return new LiteralExpression { Value = val };
		}

		public override string ToString() {
			return Value.ToString("x8") + "h";
		}
	}
}