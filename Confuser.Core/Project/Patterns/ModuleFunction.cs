using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class ModuleFunction : PatternFunction
    {
        public const string FnName = "module";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            if (!(definition is IOwnerModule))
                return false;
            object name = Arguments[0].Evaluate(definition);
            return ((IOwnerModule)definition).Module.Name == name.ToString();
        }
    }
}
