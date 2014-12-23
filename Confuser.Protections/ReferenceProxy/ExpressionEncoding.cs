using System;
using System.Collections.Generic;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal class ExpressionEncoding : IRPEncoding {
		readonly Dictionary<MethodDef, Tuple<Expression, Func<int, int>>> keys = new Dictionary<MethodDef, Tuple<Expression, Func<int, int>>>();

		public Instruction[] EmitDecode(MethodDef init, RPContext ctx, Instruction[] arg) {
			Tuple<Expression, Func<int, int>> key = GetKey(ctx, init);

			var invCompiled = new List<Instruction>();
			new CodeGen(arg, ctx.Method, invCompiled).GenerateCIL(key.Item1);
			init.Body.MaxStack += (ushort)ctx.Depth;
			return invCompiled.ToArray();
		}

		public int Encode(MethodDef init, RPContext ctx, int value) {
			Tuple<Expression, Func<int, int>> key = GetKey(ctx, init);
			return key.Item2(value);
		}

		void Compile(RPContext ctx, CilBody body, out Func<int, int> expCompiled, out Expression inverse) {
			var var = new Variable("{VAR}");
			var result = new Variable("{RESULT}");

			Expression expression;
			ctx.DynCipher.GenerateExpressionPair(
				ctx.Random,
				new VariableExpression { Variable = var }, new VariableExpression { Variable = result },
				ctx.Depth, out expression, out inverse);

			expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
				.GenerateCIL(expression)
				.Compile<Func<int, int>>();
		}

		Tuple<Expression, Func<int, int>> GetKey(RPContext ctx, MethodDef init) {
			Tuple<Expression, Func<int, int>> ret;
			if (!keys.TryGetValue(init, out ret)) {
				Func<int, int> keyFunc;
				Expression inverse;
				Compile(ctx, init.Body, out keyFunc, out inverse);
				keys[init] = ret = Tuple.Create(inverse, keyFunc);
			}
			return ret;
		}

		class CodeGen : CILCodeGen {
			readonly Instruction[] arg;

			public CodeGen(Instruction[] arg, MethodDef method, IList<Instruction> instrs)
				: base(method, instrs) {
				this.arg = arg;
			}

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (Instruction instr in arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}
	}
}