using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class MemberTypeFunction : PatternFunction
    {
        public const string FnName = "member-type";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            object type = Arguments[0].Evaluate(definition);

            if (definition is TypeDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "type") == 0;

            else if (definition is MethodDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "method") == 0;

            else if (definition is FieldDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "field") == 0;

            else if (definition is PropertyDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "property") == 0;

            else if (definition is EventDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "event") == 0;

            else if (definition is ModuleDef)
                return StringComparer.OrdinalIgnoreCase.Compare(type.ToString(), "module") == 0;

            return false;
        }
    }
}
