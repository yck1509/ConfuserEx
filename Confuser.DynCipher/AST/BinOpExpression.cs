using System;

namespace Confuser.DynCipher.AST {
	public enum BinOps {
		Add,
		Sub,
		Div,
		Mul,
		Or,
		And,
		Xor,
		Lsh,
		Rsh
	}

	public class BinOpExpression : Expression {
		public Expression Left { get; set; }
		public Expression Right { get; set; }
		public BinOps Operation { get; set; }

		public override string ToString() {
			string op;
			switch (Operation) {
				case BinOps.Add:
					op = "+";
					break;
				case BinOps.Sub:
					op = "-";
					break;
				case BinOps.Div:
					op = "/";
					break;
				case BinOps.Mul:
					op = "*";
					break;
				case BinOps.Or:
					op = "|";
					break;
				case BinOps.And:
					op = "&";
					break;
				case BinOps.Xor:
					op = "^";
					break;
				case BinOps.Lsh:
					op = "<<";
					break;
				case BinOps.Rsh:
					op = ">>";
					break;
				default:
					throw new Exception();
			}
			return string.Format("({0} {1} {2})", Left, op, Right);
		}
	}
}