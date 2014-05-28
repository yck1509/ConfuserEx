using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal class ExpressionPredicate : IPredicate {
		private readonly CFContext ctx;
		private Func<int, int> expCompiled;
		private Expression expression;

		private bool inited;
		private List<Instruction> invCompiled;
		private Expression inverse;
		private Local stateVar;

		public ExpressionPredicate(CFContext ctx) {
			this.ctx = ctx;
		}

		public void Init(CilBody body) {
			if (inited)
				return;
			stateVar = new Local(ctx.Method.Module.CorLibTypes.Int32);
			body.Variables.Add(stateVar);
			body.InitLocals = true;
			Compile(body);
			inited = true;
		}

		public void EmitSwitchLoad(IList<Instruction> instrs) {
			instrs.Add(Instruction.Create(OpCodes.Stloc, stateVar));
			foreach (Instruction instr in invCompiled)
				instrs.Add(instr);
		}

		public int GetSwitchKey(int key) {
			return expCompiled(key);
		}

		private void Compile(CilBody body) {
			var var = new Variable("{VAR}");
			var result = new Variable("{RESULT}");

			ctx.DynCipher.GenerateExpressionPair(
				ctx.Random,
				new VariableExpression { Variable = var }, new VariableExpression { Variable = result },
				ctx.Depth, out expression, out inverse);

			expCompiled = new DMCodeGen(typeof (int), new[] { Tuple.Create("{VAR}", typeof (int)) })
				.GenerateCIL(expression)
				.Compile<Func<int, int>>();

			invCompiled = new List<Instruction>();
			new CodeGen(stateVar, ctx, invCompiled).GenerateCIL(inverse);
			body.MaxStack += (ushort)ctx.Depth;
		}

		private class CodeGen : CILCodeGen {
			private readonly Local state;

			public CodeGen(Local state, CFContext ctx, IList<Instruction> instrs)
				: base(ctx.Method, instrs) {
				this.state = state;
			}

			protected override Local Var(Variable var) {
				if (var.Name == "{RESULT}")
					return state;
				return base.Var(var);
			}
		}
	}
}