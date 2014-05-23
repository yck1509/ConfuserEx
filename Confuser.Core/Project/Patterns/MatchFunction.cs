using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.Text.RegularExpressions;

namespace Confuser.Core.Project.Patterns
{
    class MatchFunction : PatternFunction
    {
        public const string FnName = "match";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            var regex = Arguments[0].Evaluate(definition).ToString();
            return Regex.IsMatch(definition.FullName, regex);
        }
    }
    class MatchNameFunction : PatternFunction
    {
        public const string FnName = "match-name";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            var regex = Arguments[0].Evaluate(definition).ToString();
            return Regex.IsMatch(definition.Name, regex);
        }
    }
    class MatchTypeNameFunction : PatternFunction
    {
        public const string FnName = "match-type-name";
        public override string Name { get { return FnName; } }

        public override int ArgumentCount { get { return 1; } }

        public override object Evaluate(IDnlibDef definition)
        {
            if (definition is TypeDef)
            {
                var regex = Arguments[0].Evaluate(definition).ToString();
                return Regex.IsMatch(definition.Name, regex);
            }
            else if (definition is IMemberDef)
            {
                var regex = Arguments[0].Evaluate(definition).ToString();
                return Regex.IsMatch(((IMemberDef)definition).DeclaringType.Name, regex);
            }
            return false;
        }
    }
}
