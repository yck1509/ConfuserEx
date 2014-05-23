using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class DeclTypeFunction : PatternFunction
    {
        public const string FnName = "decl-type";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            if (!(definition is IMemberDef))
                return false;
            object fullName = Arguments[0].Evaluate(definition);
            return ((IMemberDef)definition).DeclaringType.FullName == fullName.ToString();
        }
    }
}
