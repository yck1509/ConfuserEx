using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Transforms
{
    class ExpansionTransform
    {
        static bool ProcessStatement(Statement st, StatementBlock block)
        {
            if (st is AssignmentStatement)
            {
                AssignmentStatement assign = (AssignmentStatement)st;
                if (assign.Value is BinOpExpression)
                {
                    BinOpExpression exp = (BinOpExpression)assign.Value;
                    if ((exp.Left is BinOpExpression || exp.Right is BinOpExpression) &&
                        exp.Left != assign.Target)
                    {
                        block.Statements.Add(new AssignmentStatement()
                        {
                            Target = assign.Target,
                            Value = exp.Left
                        });
                        block.Statements.Add(new AssignmentStatement()
                        {
                            Target = assign.Target,
                            Value = new BinOpExpression()
                            {
                                Left = assign.Target,
                                Operation = exp.Operation,
                                Right = exp.Right
                            }
                        });
                        return true;
                    }
                }
            }
            block.Statements.Add(st);
            return false;
        }

        public static void Run(StatementBlock block)
        {
            bool workDone;
            do
            {
                workDone = false;
                var copy = block.Statements.ToArray();
                block.Statements.Clear();
                foreach (var st in copy)
                    workDone |= ProcessStatement(st, block);
            } while (workDone);
        }
    }
}
