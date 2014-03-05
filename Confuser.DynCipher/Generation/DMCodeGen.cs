using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;
using System.Reflection.Emit;
using Confuser.Core;

namespace Confuser.DynCipher.Generation
{
    public class DMCodeGen
    {
        DynamicMethod dm;
        Dictionary<string, int> paramMap;
        ILGenerator ilGen;

        public DMCodeGen(Type returnType, Tuple<string, Type>[] parameters)
        {
            dm = new DynamicMethod("", returnType, parameters.Select(param => param.Item2).ToArray(), true);
            paramMap = new Dictionary<string, int>();
            for (int i = 0; i < parameters.Length; i++)
                paramMap.Add(parameters[i].Item1, i);
            ilGen = dm.GetILGenerator();
        }

        Dictionary<string, LocalBuilder> localMap = new Dictionary<string, LocalBuilder>();
        protected virtual LocalBuilder Var(Variable var)
        {
            LocalBuilder ret;
            if (!localMap.TryGetValue(var.Name, out ret))
            {
                ret = ilGen.DeclareLocal(typeof(int));
                localMap[var.Name] = ret;
            }
            return ret;
        }

        protected virtual void LoadVar(Variable var)
        {
            if (paramMap.ContainsKey(var.Name))
                ilGen.Emit(OpCodes.Ldarg, paramMap[var.Name]);
            else
                ilGen.Emit(OpCodes.Ldloc, Var(var));
        }

        protected virtual void StoreVar(Variable var)
        {
            if (paramMap.ContainsKey(var.Name))
                ilGen.Emit(OpCodes.Starg, paramMap[var.Name]);
            else
                ilGen.Emit(OpCodes.Stloc, Var(var));
        }

        public T Compile<T>()
        {
            ilGen.Emit(OpCodes.Ret);
            return (T)(object)dm.CreateDelegate(typeof(T));
        }


        public DMCodeGen GenerateCIL(Expression expression)
        {
            EmitLoad(expression);
            return this;
        }

        public DMCodeGen GenerateCIL(Statement statement)
        {
            EmitStatement(statement);
            return this;
        }

        void EmitLoad(Expression exp)
        {
            if (exp is ArrayIndexExpression)
            {
                ArrayIndexExpression arrIndex = (ArrayIndexExpression)exp;
                EmitLoad(arrIndex.Array);
                ilGen.Emit(OpCodes.Ldc_I4, arrIndex.Index);
                ilGen.Emit(OpCodes.Ldelem_U4);
            }
            else if (exp is BinOpExpression)
            {
                BinOpExpression binOp = (BinOpExpression)exp;
                EmitLoad(binOp.Left);
                EmitLoad(binOp.Right);
                OpCode op;
                switch (binOp.Operation)
                {
                    case BinOps.Add: op = OpCodes.Add; break;
                    case BinOps.Sub: op = OpCodes.Sub; break;
                    case BinOps.Div: op = OpCodes.Div; break;
                    case BinOps.Mul: op = OpCodes.Mul; break;
                    case BinOps.Or: op = OpCodes.Or; break;
                    case BinOps.And: op = OpCodes.And; break;
                    case BinOps.Xor: op = OpCodes.Xor; break;
                    case BinOps.Lsh: op = OpCodes.Shl; break;
                    case BinOps.Rsh: op = OpCodes.Shr_Un; break;
                    default: throw new NotSupportedException();
                }
                ilGen.Emit(op);
            }
            else if (exp is UnaryOpExpression)
            {
                UnaryOpExpression unaryOp = (UnaryOpExpression)exp;
                EmitLoad(unaryOp.Value);
                OpCode op;
                switch (unaryOp.Operation)
                {
                    case UnaryOps.Not: op = OpCodes.Not; break;
                    case UnaryOps.Negate: op = OpCodes.Neg; break;
                    default: throw new NotSupportedException();
                }
                ilGen.Emit(op);
            }
            else if (exp is LiteralExpression)
            {
                LiteralExpression literal = (LiteralExpression)exp;
                ilGen.Emit(OpCodes.Ldc_I4, (int)literal.Value);
            }
            else if (exp is VariableExpression)
            {
                VariableExpression var = (VariableExpression)exp;
                LoadVar(var.Variable);
            }
            else
                throw new NotSupportedException();
        }

        void EmitStore(Expression exp, Expression value)
        {
            if (exp is ArrayIndexExpression)
            {
                ArrayIndexExpression arrIndex = (ArrayIndexExpression)exp;
                EmitLoad(arrIndex.Array);
                ilGen.Emit(OpCodes.Ldc_I4, arrIndex.Index);
                EmitLoad(value);
                ilGen.Emit(OpCodes.Stelem_I4);
            }
            else if (exp is VariableExpression)
            {
                VariableExpression var = (VariableExpression)exp;
                EmitLoad(value);
                StoreVar(var.Variable);
            }
            else
                throw new NotSupportedException();
        }

        void EmitStatement(Statement statement)
        {
            if (statement is AssignmentStatement)
            {
                AssignmentStatement assignment = (AssignmentStatement)statement;
                EmitStore(assignment.Target, assignment.Value);
            }
            else if (statement is LoopStatement)
            {
                LoopStatement loop = (LoopStatement)statement;
                /*
                 *      ldc.i4  begin
                 *      br      cmp
                 *      ldc.i4  dummy   //hint for dnlib
                 * lop: nop
                 *      ...
                 *      ...
                 *      ldc.i4.1
                 *      add
                 * cmp: dup
                 *      ldc.i4  limit
                 *      blt     lop
                 *      pop
                 */
                var lbl = ilGen.DefineLabel();
                var dup = ilGen.DefineLabel();
                ilGen.Emit(OpCodes.Ldc_I4, loop.Begin);
                ilGen.Emit(OpCodes.Br, dup);
                ilGen.Emit(OpCodes.Ldc_I4, loop.Begin);
                ilGen.MarkLabel(lbl);

                foreach (var child in loop.Statements)
                    EmitStatement(child);

                ilGen.Emit(OpCodes.Ldc_I4_1);
                ilGen.Emit(OpCodes.Add);
                ilGen.MarkLabel(dup);
                ilGen.Emit(OpCodes.Dup);
                ilGen.Emit(OpCodes.Ldc_I4, loop.Limit);
                ilGen.Emit(OpCodes.Blt, lbl);
                ilGen.Emit(OpCodes.Pop);
            }
            else if (statement is StatementBlock)
            {
                foreach (var child in ((StatementBlock)statement).Statements)
                    EmitStatement(child);
            }
            else
                throw new NotSupportedException();
        }
    }
}
