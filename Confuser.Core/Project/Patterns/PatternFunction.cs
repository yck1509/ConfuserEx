using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core.Project.Patterns
{
    public abstract class PatternFunction : PatternExpression
    {
        public abstract string Name { get; }
        public abstract int ArgumentCount { get; }
        public IList<PatternExpression> Arguments { get; set; }

        public override void Serialize(IList<PatternToken> tokens)
        {
            tokens.Add(new PatternToken(TokenType.Identifier, Name));
            tokens.Add(new PatternToken(TokenType.LParens));
            for (int i = 0; i < Arguments.Count; i++)
            {
                if (i != 0)
                    tokens.Add(new PatternToken(TokenType.Comma));
                Arguments[i].Serialize(tokens);
            }
            tokens.Add(new PatternToken(TokenType.RParens));
        }
    }
}
