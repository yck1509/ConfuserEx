using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;
using dnlib.DotNet.Emit;

namespace Confuser.DynCipher.AST
{
    public class LiteralExpression : Expression
    {
        public uint Value { get; set; }

        public static implicit operator LiteralExpression(uint val)
        {
            return new LiteralExpression() { Value = val };
        }

        public override string ToString()
        {
            return Value.ToString("x8") + "h";
        }
    }
}
