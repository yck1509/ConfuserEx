using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    class NameFunction : PatternFunction
    {
        public const string FnName = "name";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            object name = Arguments[0].Evaluate(definition);
            return definition.Name == name.ToString();
        }
    }
}
