using System;
using System.Collections.Generic;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Generation {
	internal class CipherGenContext {
		readonly Variable[] dataVars;
		readonly Variable keyVar = new Variable("{KEY}");
		readonly RandomGenerator random;
		readonly List<Variable> tempVars = new List<Variable>();
		int tempVarCounter;

		public CipherGenContext(RandomGenerator random, int dataVarCount) {
			this.random = random;
			Block = new StatementBlock(); // new LoopStatement() { Begin = 0, Limit = 4 };
			dataVars = new Variable[dataVarCount];
			for (int i = 0; i < dataVarCount; i++)
				dataVars[i] = new Variable("v" + i) { Tag = i };
		}

		public StatementBlock Block { get; private set; }

		public Expression GetDataExpression(int index) {
			return new VariableExpression { Variable = dataVars[index] };
		}

		public Expression GetKeyExpression(int index) {
			return new ArrayIndexExpression {
				Array = new VariableExpression { Variable = keyVar },
				Index = index
			};
		}

		public CipherGenContext Emit(Statement statement) {
			Block.Statements.Add(statement);
			return this;
		}

		public IDisposable AcquireTempVar(out VariableExpression exp) {
			Variable var;
			if (tempVars.Count == 0)
				var = new Variable("t" + tempVarCounter++);
			else {
				var = tempVars[random.NextInt32(tempVars.Count)];
				tempVars.Remove(var);
			}
			exp = new VariableExpression { Variable = var };
			return new TempVarHolder(this, var);
		}

		struct TempVarHolder : IDisposable {
			readonly CipherGenContext parent;
			readonly Variable tempVar;

			public TempVarHolder(CipherGenContext p, Variable v) {
				parent = p;
				tempVar = v;
			}

			public void Dispose() {
				parent.tempVars.Add(tempVar);
			}
		}
	}
}