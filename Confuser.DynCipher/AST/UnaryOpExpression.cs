using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;
using dnlib.DotNet.Emit;

namespace Confuser.DynCipher.AST
{
    public enum UnaryOps
    {
        Not,
        Negate
    }

    public class UnaryOpExpression : Expression
    {
        public Expression Value { get; set; }
        public UnaryOps Operation { get; set; }

        public override string ToString()
        {
            string op;
            switch (Operation)
            {
                case UnaryOps.Not: op = "~"; break;
                case UnaryOps.Negate: op = "-"; break;
                default: throw new Exception();
            }
            return op + Value.ToString();
        }
    }
}
