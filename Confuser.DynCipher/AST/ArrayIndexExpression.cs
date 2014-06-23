using System;

namespace Confuser.DynCipher.AST {
	public class ArrayIndexExpression : Expression {
		public Expression Array { get; set; }
		public int Index { get; set; }

		public override string ToString() {
			return string.Format("{0}[{1}]", Array, Index);
		}
	}
}