using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.AST
{
    public class VariableExpression : Expression
    {
        public Variable Variable { get; set; }
        
        public override string ToString()
        {
            return Variable.Name;
        }
    }
}
