using System;
using System.Text;

namespace Confuser.DynCipher.AST {
	// i.e. for loop
	public class LoopStatement : StatementBlock {
		public int Begin { get; set; }
		public int Limit { get; set; }

		public override string ToString() {
			var ret = new StringBuilder();
			ret.AppendFormat("for (int i = {0}; i < {1}; i++)", Begin, Limit);
			ret.AppendLine();
			ret.Append(base.ToString());
			return ret.ToString();
		}
	}
}