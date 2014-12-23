using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Transforms {
	internal class MulToShiftTransform {
		static uint NumberOfSetBits(uint i) {
			i = i - ((i >> 1) & 0x55555555);
			i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
			return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
		}

		static Expression ProcessExpression(Expression exp) {
			if (exp is BinOpExpression) {
				var binOp = (BinOpExpression)exp;
				if (binOp.Operation == BinOps.Mul && binOp.Right is LiteralExpression) {
					// Decompose multiplication into shifts, e.g. x * 3 => x << 1 + x
					uint literal = ((LiteralExpression)binOp.Right).Value;
					if (literal == 0) return (LiteralExpression)0;
					if (literal == 1) return binOp.Left;

					uint bits = NumberOfSetBits(literal);
					if (bits <= 2) {
						var sum = new List<Expression>();
						int n = 0;
						while (literal != 0) {
							if ((literal & 1) != 0) {
								if (n == 0)
									sum.Add(binOp.Left);
								else
									sum.Add(binOp.Left << n);
							}
							literal >>= 1;
							n++;
						}
						BinOpExpression x = sum.OfType<BinOpExpression>().First();
						foreach (Expression i in sum.Except(new[] { x }))
							x += i;
						return x;
					}
				}
				else {
					binOp.Left = ProcessExpression(binOp.Left);
					binOp.Right = ProcessExpression(binOp.Right);
				}
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