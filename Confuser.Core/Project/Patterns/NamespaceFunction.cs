using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class NamespaceFunction : PatternFunction
    {
        public const string FnName = "namespace";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            if (!(definition is TypeDef) && !(definition is IMemberDef))
                return false;
            object ns = Arguments[0].Evaluate(definition);
            var type = definition as TypeDef;
            if (type == null)
                type = ((IMemberDef)definition).DeclaringType;
            return type.Namespace == ns.ToString();
        }
    }
}
