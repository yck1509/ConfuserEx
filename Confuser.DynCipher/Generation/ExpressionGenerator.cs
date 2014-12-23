using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Generation {
	internal class ExpressionGenerator {
		static Expression GenerateExpression(RandomGenerator random, Expression current, int currentDepth, int targetDepth) {
			if (currentDepth == targetDepth || (currentDepth > targetDepth / 3 && random.NextInt32(100) > 85))
				return current;

			switch ((ExpressionOps)random.NextInt32(6)) {
				case ExpressionOps.Add:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) +
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1, targetDepth);

				case ExpressionOps.Sub:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) -
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1, targetDepth);

				case ExpressionOps.Mul:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) * (LiteralExpression)(random.NextUInt32() | 1);

				case ExpressionOps.Xor:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) ^
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1, targetDepth);

				case ExpressionOps.Not:
					return ~GenerateExpression(random, current, currentDepth + 1, targetDepth);

				case ExpressionOps.Neg:
					return -GenerateExpression(random, current, currentDepth + 1, targetDepth);
			}
			throw new UnreachableException();
		}

		static void SwapOperands(RandomGenerator random, Expression exp) {
			if (exp is BinOpExpression) {
				var binExp = (BinOpExpression)exp;
				if (random.NextBoolean()) {
					Expression tmp = binExp.Left;
					binExp.Left = binExp.Right;
					binExp.Right = tmp;
				}
				SwapOperands(random, binExp.Left);
				SwapOperands(random, binExp.Right);
			}
			else if (exp is UnaryOpExpression)
				SwapOperands(random, ((UnaryOpExpression)exp).Value);
			else if (exp is LiteralExpression || exp is VariableExpression)
				return;
			else
				throw new UnreachableException();
		}

		static bool HasVariable(Expression exp, Dictionary<Expression, bool> hasVar) {
			bool ret;
			if (!hasVar.TryGetValue(exp, out ret)) {
				if (exp is VariableExpression)
					ret = true;
				else if (exp is LiteralExpression)
					ret = false;
				else if (exp is BinOpExpression) {
					var binExp = (BinOpExpression)exp;
					ret = HasVariable(binExp.Left, hasVar) || HasVariable(binExp.Right, hasVar);
				}
				else if (exp is UnaryOpExpression) {
					ret = HasVariable(((UnaryOpExpression)exp).Value, hasVar);
				}
				else
					throw new UnreachableException();
				hasVar[exp] = ret;
			}
			return ret;
		}

		static Expression GenerateInverse(Expression exp, Expression var, Dictionary<Expression, bool> hasVar) {
			Expression result = var;
			while (!(exp is VariableExpression)) {
				Debug.Assert(hasVar[exp]);
				if (exp is UnaryOpExpression) {
					var unaryOp = (UnaryOpExpression)exp;
					result = new UnaryOpExpression {
						Operation = unaryOp.Operation,
						Value = result
					};
					exp = unaryOp.Value;
				}
				else if (exp is BinOpExpression) {
					var binOp = (BinOpExpression)exp;
					bool leftHasVar = hasVar[binOp.Left];
					Expression varExp = leftHasVar ? binOp.Left : binOp.Right;
					Expression constExp = leftHasVar ? binOp.Right : binOp.Left;

					if (binOp.Operation == BinOps.Add)
						result = new BinOpExpression {
							Operation = BinOps.Sub,
							Left = result,
							Right = constExp
						};

					else if (binOp.Operation == BinOps.Sub) {
						if (leftHasVar) {
							// v - k = r => v = r + k
							result = new BinOpExpression {
								Operation = BinOps.Add,
								Left = result,
								Right = constExp
							};
						}
						else {
							// k - v = r => v = k - r
							result = new BinOpExpression {
								Operation = BinOps.Sub,
								Left = constExp,
								Right = result
							};
						}
					}
					else if (binOp.Operation == BinOps.Mul) {
						Debug.Assert(constExp is LiteralExpression);
						uint val = ((LiteralExpression)constExp).Value;
						val = MathsUtils.modInv(val);
						result = new BinOpExpression {
							Operation = BinOps.Mul,
							Left = result,
							Right = (LiteralExpression)val
						};
					}
					else if (binOp.Operation == BinOps.Xor)
						result = new BinOpExpression {
							Operation = BinOps.Xor,
							Left = result,
							Right = constExp
						};

					exp = varExp;
				}
			}
			return result;
		}

		public static void GeneratePair(RandomGenerator random, Expression var, Expression result, int depth, out Expression expression, out Expression inverse) {
			expression = GenerateExpression(random, var, 0, depth);
			SwapOperands(random, expression);

			var hasVar = new Dictionary<Expression, bool>();
			HasVariable(expression, hasVar);

			inverse = GenerateInverse(expression, result, hasVar);
		}

		enum ExpressionOps {
			Add,
			Sub,
			Mul,
			Xor,
			Not,
			Neg
		}
	}
}