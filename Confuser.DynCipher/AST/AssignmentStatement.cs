using System;

namespace Confuser.DynCipher.AST {
	public class AssignmentStatement : Statement {
		public Expression Target { get; set; }
		public Expression Value { get; set; }

		public override string ToString() {
			return string.Format("{0} = {1};", Target, Value);
		}
	}
}