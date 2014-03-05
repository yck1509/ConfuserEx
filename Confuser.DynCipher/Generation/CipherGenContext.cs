using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;
using Confuser.Core.Services;

namespace Confuser.DynCipher.Generation
{
    class CipherGenContext
    {
        RandomGenerator random;
        public CipherGenContext(RandomGenerator random, int dataVarCount)
        {
            this.random = random;
            Block = new LoopStatement() { Begin = 0, Limit = 4 };
            dataVars = new Variable[dataVarCount];
            for (int i = 0; i < dataVarCount; i++)
                dataVars[i] = new Variable("v" + i) { Tag = i };
        }
        public StatementBlock Block { get; private set; }

        Variable[] dataVars;
        Variable keyVar = new Variable("{KEY}");

        public Expression GetDataExpression(int index)
        {
            return new VariableExpression() { Variable = dataVars[index] };
        }

        public Expression GetKeyExpression(int index)
        {
            return new ArrayIndexExpression()
            {
                Array = new VariableExpression() { Variable = keyVar },
                Index = index
            };
        }

        public CipherGenContext Emit(Statement statement)
        {
            Block.Statements.Add(statement);
            return this;
        }

        struct TempVarHolder : IDisposable
        {
            public TempVarHolder(CipherGenContext p, Variable v)
            {
                this.parent = p;
                this.tempVar = v;
            }
            CipherGenContext parent;
            Variable tempVar;

            public void Dispose()
            {
                parent.tempVars.Add(tempVar);
            }
        }

        int tempVarCounter = 0;
        List<Variable> tempVars = new List<Variable>();
        public IDisposable AcquireTempVar(out VariableExpression exp)
        {
            Variable var;
            if (tempVars.Count == 0)
                var = new Variable("t" + tempVarCounter++);
            else
            {
                var = tempVars[random.NextInt32(tempVars.Count)];
                tempVars.Remove(var);
            }
            exp = new VariableExpression() { Variable = var };
            return new TempVarHolder(this, var);
        }

    }
}
