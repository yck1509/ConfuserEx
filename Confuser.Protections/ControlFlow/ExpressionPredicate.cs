using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Core;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow
{
    class ExpressionPredicate : IPredicate
    {
        CFContext ctx;
        Local stateVar;
        public ExpressionPredicate(CFContext ctx)
        {
            this.ctx = ctx;
        }

        Expression expression;
        Expression inverse;
        Func<int, int> expCompiled;
        List<Instruction> invCompiled;

        class CodeGen : CILCodeGen
        {
            Local state;
            public CodeGen(Local state, CFContext ctx, IList<Instruction> instrs)
                : base(ctx.Method, instrs)
            {
                this.state = state;
            }
            protected override Local Var(Variable var)
            {
                if (var.Name == "{RESULT}")
                    return state;
                else
                    return base.Var(var);
            }
        }

        void Compile(CilBody body)
        {
            Variable var = new Variable("{VAR}");
            Variable result = new Variable("{RESULT}");

            ctx.DynCipher.GenerateExpressionPair(
                ctx.Random,
                new VariableExpression() { Variable = var }, new VariableExpression() { Variable = result },
                ctx.Depth, out expression, out inverse);

            expCompiled = new DMCodeGen(typeof(int), new[] { Tuple.Create("{VAR}", typeof(int)) })
                .GenerateCIL(expression)
                .Compile<Func<int, int>>();

            invCompiled = new List<Instruction>();
            new CodeGen(stateVar, ctx, invCompiled).GenerateCIL(inverse);
        }

        bool inited = false;
        public void Init(CilBody body)
        {
            if (inited)
                return;
            stateVar = new Local(ctx.Method.Module.CorLibTypes.Int32);
            body.Variables.Add(stateVar);
            body.InitLocals = true;
            Compile(body);
            inited = true;
        }

        public void EmitSwitchLoad(IList<Instruction> instrs)
        {
            instrs.Add(Instruction.Create(OpCodes.Stloc, stateVar));
            foreach (var instr in invCompiled)
                instrs.Add(instr);
        }

        public int GetSwitchKey(int key)
        {
            return expCompiled(key);
        }
    }
}
