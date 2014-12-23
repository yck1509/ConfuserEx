using System;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Transforms {
	internal class NormalizeBinOpTransform {
		static Expression ProcessExpression(Expression exp) {
			if (exp is BinOpExpression) {
				var binOp = (BinOpExpression)exp;
				var binOpRight = binOp.Right as BinOpExpression;
				//  a + (b + c) => (a + b) + c
				if (binOpRight != null && binOpRight.Operation == binOp.Operation &&
				    (binOp.Operation == BinOps.Add || binOp.Operation == BinOps.Mul ||
				     binOp.Operation == BinOps.Or || binOp.Operation == BinOps.And ||
				     binOp.Operation == BinOps.Xor)) {
					binOp.Left = new BinOpExpression {
						Left = binOp.Left,
						Operation = binOp.Operation,
						Right = binOpRight.Left
					};
					binOp.Right = binOpRight.Right;
				}

				binOp.Left = ProcessExpression(binOp.Left);
				binOp.Right = ProcessExpression(binOp.Right);

				if (binOp.Right is LiteralExpression && ((LiteralExpression)binOp.Right).Value == 0 &&
				    binOp.Operation == BinOps.Add) // x + 0 => x
					return binOp.Left;
			}
			else if (exp is ArrayIndexExpression) {
				((ArrayIndexExpression)exp).Array = ProcessExpression(((ArrayIndexExpression)exp).Array);
			}
			else if (exp is UnaryOpExpression) {
				((UnaryOpExpression)exp).Value = ProcessExpression(((UnaryOpExpression)exp).Value);
			}
			return exp;
		}

		static void ProcessStatement(Statement st) {
			if (st is AssignmentStatement) {
				var assign = (AssignmentStatement)st;
				assign.Target = ProcessExpression(assign.Target);
				assign.Value = ProcessExpression(assign.Value);
			}
		}

		public static void Run(StatementBlock block) {
			foreach (Statement st in block.Statements)
				ProcessStatement(st);
		}
	}
}