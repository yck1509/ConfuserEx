using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    public abstract class PatternExpression
    {
        public abstract object Evaluate(IDnlibDef definition);
        public abstract void Serialize(IList<PatternToken> tokens);
    }
}
