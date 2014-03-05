using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.AST
{
    public class AssignmentStatement : Statement
    {
        public Expression Target { get; set; }
        public Expression Value { get; set; }

        public override string ToString()
        {
            return string.Format("{0} = {1};", Target, Value);
        }
    }
}
