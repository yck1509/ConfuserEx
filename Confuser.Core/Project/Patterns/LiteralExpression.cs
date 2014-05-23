using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    public class LiteralExpression : PatternExpression
    {
        public LiteralExpression(object literal)
        {
            this.Literal = literal;
        }
        public object Literal { get; private set; }

        public override object Evaluate(IDnlibDef definition)
        {
            return Literal;
        }

        public override void Serialize(IList<PatternToken> tokens)
        {
            if (Literal is bool)
            {
                bool value = (bool)Literal;
                tokens.Add(new PatternToken(TokenType.Identifier, value.ToString().ToLowerInvariant()));
            }
            else
                tokens.Add(new PatternToken(TokenType.Literal, Literal.ToString()));
        }
    }
}
