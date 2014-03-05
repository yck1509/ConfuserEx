using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;
using dnlib.DotNet.Emit;

namespace Confuser.DynCipher.AST
{
    public class ArrayIndexExpression : Expression
    {
        public ArrayIndexExpression()
        {
        }

        public Expression Array { get; set; }
        public int Index { get; set; }

        public override string ToString()
        {
            return string.Format("{0}[{1}]", Array, Index);
        }
    }
}
