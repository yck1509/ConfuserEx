using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class NotOperator : PatternOperator
    {
        public const string OpName = "not";
        public override string Name { get { return OpName; } }

        public override bool IsUnary { get { return true; } }

        public override object Evaluate(IDnlibDef definition)
        {
            return !(bool)ArgumentA.Evaluate(definition);
        }
    }
}
