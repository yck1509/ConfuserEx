using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class AndOperator : PatternOperator
    {
        public const string OpName = "and";
        public override string Name { get { return OpName; } }

        public override bool IsUnary { get { return false; } }

        public override object Evaluate(IDnlibDef definition)
        {
            bool a = (bool)ArgumentA.Evaluate(definition);
            if (!a) return false;
            return (bool)ArgumentB.Evaluate(definition);
        }
    }
}
